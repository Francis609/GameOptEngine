FROM rust:1.100-alpine AS build
RUN apk add --no-cache musl-dev
WORKDIR /src
COPY Cargo.toml Cargo.lock ./
COPY src ./src
RUN cargo build --release --locked

FROM alpine:3.24
RUN apk add --no-cache ca-certificates && addgroup -S app && adduser -S -G app app
WORKDIR /app
COPY --from=build /src/target/release/gameoptengine /usr/local/bin/gameoptengine
USER app
ENV GAMEOPTENGINE_ACTIVITY_LOG_PATH=/var/lib/gameoptengine/activity.jsonl
EXPOSE 5088
ENTRYPOINT ["/usr/local/bin/gameoptengine", "run"]
