use axum::{Json, Router, extract::State, routing::get};
use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use std::{
    collections::{HashMap, HashSet},
    env,
    path::{Path, PathBuf},
    sync::Arc,
    time::Duration,
};
use sysinfo::{ProcessesToUpdate, System};
use tokio::sync::Mutex;

#[derive(Clone, Debug, Serialize, Deserialize)]
struct ActivityRecord {
    index: u64,
    timestamp_utc: DateTime<Utc>,
    activity: String,
    details: String,
}

struct ActivityLog {
    path: PathBuf,
    records: Mutex<Vec<ActivityRecord>>,
}
impl ActivityLog {
    fn open(path: PathBuf) -> Self {
        let records = std::fs::read_to_string(&path)
            .unwrap_or_default()
            .lines()
            .filter_map(|line| serde_json::from_str::<ActivityRecord>(line).ok())
            .collect();
        Self {
            path,
            records: Mutex::new(records),
        }
    }
    async fn record(&self, activity: &str, details: String) -> Result<ActivityRecord, String> {
        let mut records = self.records.lock().await;
        let persisted_last = tokio::fs::read_to_string(&self.path)
            .await
            .ok()
            .map(|contents| {
                contents
                    .lines()
                    .filter_map(|line| serde_json::from_str::<ActivityRecord>(line).ok())
                    .map(|record| record.index)
                    .max()
                    .unwrap_or(0)
            })
            .unwrap_or(0);
        let row = ActivityRecord {
            index: records
                .last()
                .map_or(0, |record| record.index)
                .max(persisted_last)
                + 1,
            timestamp_utc: Utc::now(),
            activity: activity.into(),
            details,
        };
        if let Some(parent) = self.path.parent() {
            tokio::fs::create_dir_all(parent)
                .await
                .map_err(|e| e.to_string())?;
        }
        let mut line = serde_json::to_vec(&row).map_err(|e| e.to_string())?;
        line.push(b'\n');
        use tokio::io::AsyncWriteExt;
        let mut file = tokio::fs::OpenOptions::new()
            .create(true)
            .append(true)
            .open(&self.path)
            .await
            .map_err(|e| e.to_string())?;
        file.write_all(&line).await.map_err(|e| e.to_string())?;
        records.push(row.clone());
        Ok(row)
    }
    async fn all(&self) -> Vec<ActivityRecord> {
        self.records.lock().await.clone()
    }
}
#[derive(Clone)]
struct ViewerState {
    log: Arc<ActivityLog>,
}
#[tokio::main]
async fn main() {
    let mut args = env::args().skip(1);
    let mode = args.next().unwrap_or_else(|| "run".into());
    let path = env::var_os("GAMEOPTENGINE_ACTIVITY_LOG_PATH")
        .map(PathBuf::from)
        .unwrap_or_else(default_log_path);
    let log = Arc::new(ActivityLog::open(path));
    if mode == "--help" || mode == "-h" {
        println!("GameOptEngine\nUsage: gameoptengine [run|serve|watch|log [--follow]]");
        return;
    }
    match mode.as_str() {
        "log" => {
            let follow = args.any(|a| a == "--follow");
            loop {
                for r in log.all().await {
                    println!(
                        "{} {} {} {}",
                        r.index, r.timestamp_utc, r.activity, r.details
                    );
                }
                if !follow {
                    break;
                }
                tokio::time::sleep(Duration::from_secs(2)).await;
            }
        }
        "watch" => {
            let _ = log
                .record(
                    "service.started",
                    "GameOptEngine process monitor started".into(),
                )
                .await;
            monitor(log, configured_names()).await;
        }
        "serve" => serve(log).await,
        _ => {
            let monitor_log = log.clone();
            let watcher = tokio::spawn(async move {
                let _ = monitor_log
                    .record(
                        "service.started",
                        "GameOptEngine process monitor started".into(),
                    )
                    .await;
                monitor(monitor_log, configured_names()).await;
            });
            tokio::select! {_ = serve(log)=>{}, _ = tokio::signal::ctrl_c()=>{}}
            watcher.abort();
        }
    }
}
fn default_log_path() -> PathBuf {
    let base = env::var_os("LOCALAPPDATA")
        .or_else(|| env::var_os("XDG_STATE_HOME"))
        .map(PathBuf::from)
        .unwrap_or_else(|| PathBuf::from("."));
    base.join("GameOptEngine").join("activity.jsonl")
}
async fn serve(log: Arc<ActivityLog>) {
    let bind = env::var("GAMEOPTENGINE_ACTIVITY_VIEWER_PREFIX")
        .unwrap_or_else(|_| "127.0.0.1:5088".into())
        .replace("http://", "")
        .trim_end_matches('/')
        .to_string();
    let app = Router::new()
        .route("/", get(viewer))
        .route("/api/activity", get(activity_json))
        .with_state(ViewerState { log });
    let listener = tokio::net::TcpListener::bind(&bind)
        .await
        .expect("bind viewer");
    println!("Activity viewer listening at http://{bind}/");
    axum::serve(listener, app).await.expect("serve viewer");
}
fn configured_names() -> HashSet<String> {
    let mut names = HashSet::new();
    if let Ok(extra) = env::var("GAMEOPTENGINE_GAME_PROCESS_NAMES") {
        names.extend(
            extra
                .split(',')
                .map(normalize_name)
                .filter(|s| !s.is_empty()),
        );
    }
    for root in steam_roots() {
        let common = root.join("steamapps").join("common");
        if !common.exists() {
            continue;
        }
        for entry in walkdir::WalkDir::new(common)
            .follow_links(false)
            .into_iter()
            .filter_map(Result::ok)
        {
            if entry.file_type().is_file()
                && entry
                    .path()
                    .extension()
                    .is_some_and(|ext| ext.eq_ignore_ascii_case("exe"))
            {
                let name = normalize_name(&entry.file_name().to_string_lossy());
                if ![
                    "setup",
                    "unins",
                    "crashreporter",
                    "crashhandler",
                    "vc_redist",
                    "dxsetup",
                    "eaclauncher",
                ]
                .iter()
                .any(|ignored| name.contains(ignored))
                {
                    names.insert(name);
                }
            }
        }
    }
    names
}
fn normalize_name(value: &str) -> String {
    Path::new(value)
        .file_stem()
        .unwrap_or_default()
        .to_string_lossy()
        .to_ascii_lowercase()
}
fn steam_roots() -> Vec<PathBuf> {
    let mut roots = Vec::new();
    if let Ok(path) = env::var("STEAM_DIR") {
        roots.push(PathBuf::from(path));
    }
    if cfg!(windows) {
        if let Some(path) = env::var_os("ProgramFiles(x86)") {
            roots.push(PathBuf::from(path).join("Steam"));
        }
        if let Some(path) = env::var_os("ProgramFiles") {
            roots.push(PathBuf::from(path).join("Steam"));
        }
    }
    let mut expanded = roots.clone();
    for root in roots {
        let library = root.join("steamapps").join("libraryfolders.vdf");
        if let Ok(contents) = std::fs::read_to_string(library) {
            for line in contents.lines() {
                if line.contains("\"path\"") {
                    if let Some(path) = line.split('"').nth(3) {
                        expanded.push(PathBuf::from(path.replace("\\\\", "\\")));
                    }
                }
            }
        }
    }
    expanded
}
async fn monitor(log: Arc<ActivityLog>, names: HashSet<String>) -> ! {
    let mut system = System::new_all();
    let mut active: HashMap<u32, (String, u64)> = HashMap::new();
    loop {
        system.refresh_processes(ProcessesToUpdate::All, true);
        let current: HashMap<u32, (String, u64)> = system
            .processes()
            .iter()
            .filter_map(|(pid, p)| {
                let name = normalize_name(&p.name().to_string_lossy());
                names
                    .contains(&name)
                    .then_some((pid.as_u32(), (name, p.memory())))
            })
            .collect();
        for (&pid, (name, memory)) in &current {
            if !active.contains_key(&pid) {
                let _ = log
                    .record(
                        "game.process.started",
                        format!("{name}; PID {pid}; working set {} MB", memory / 1_048_576),
                    )
                    .await;
            }
        }
        for (&pid, (name, _)) in &active {
            if !current.contains_key(&pid) {
                let _ = log
                    .record("game.process.exited", format!("{name}; PID {pid}"))
                    .await;
            }
        }
        active = current;
        tokio::time::sleep(Duration::from_secs(2)).await;
    }
}
async fn activity_json(State(state): State<ViewerState>) -> Json<Vec<ActivityRecord>> {
    Json(state.log.all().await)
}
async fn viewer(State(state): State<ViewerState>) -> String {
    let rows = state
        .log
        .all()
        .await
        .into_iter()
        .rev()
        .map(|r| {
            format!(
                "<tr><td>{}</td><td>{}</td><td>{}</td><td>{}</td></tr>",
                r.index,
                escape(&r.timestamp_utc.to_string()),
                escape(&r.activity),
                escape(&r.details)
            )
        })
        .collect::<String>();
    format!(
        "<!doctype html><html><head><meta charset='utf-8'><meta http-equiv='refresh' content='5'><title>GameOptEngine Activity</title></head><body><h1>GameOptEngine Activity</h1><p>Newest first; refreshes every five seconds. <a href='/api/activity'>JSON</a></p><table><tr><th>Index</th><th>UTC time</th><th>Activity</th><th>Details</th></tr>{rows}</table></body></html>"
    )
}
fn escape(value: &str) -> String {
    value
        .replace('&', "&amp;")
        .replace('<', "&lt;")
        .replace('>', "&gt;")
        .replace('"', "&quot;")
        .replace('\'', "&#39;")
}
#[cfg(test)]
mod tests {
    use super::*;
    #[tokio::test]
    async fn activity_indices_continue_after_reload() {
        let path = env::temp_dir().join(format!(
            "gameopt-{}.jsonl",
            Utc::now().timestamp_nanos_opt().unwrap()
        ));
        let log = ActivityLog::open(path.clone());
        assert_eq!(log.record("started", "test".into()).await.unwrap().index, 1);
        assert_eq!(
            ActivityLog::open(path.clone())
                .record("exited", "test".into())
                .await
                .unwrap()
                .index,
            2
        );
        let _ = std::fs::remove_file(path);
    }
    #[test]
    fn html_escapes_untrusted_activity() {
        assert_eq!(escape("<script>&\""), "&lt;script&gt;&amp;&quot;");
    }
    #[test]
    fn names_strip_exe_and_casefold() {
        assert_eq!(normalize_name("GameA.EXE"), "gamea");
    }
}
