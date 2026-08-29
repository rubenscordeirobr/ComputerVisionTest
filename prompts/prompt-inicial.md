# Minimal Prototype — Multi-Camera Object Detection, Annotation & Recording

## Goal
Build a minimal working prototype that connects to multiple cameras, runs YOLO26n
detection on their frames, overlays annotations, restreams the annotated video
through MediaMTX, and records clips of tracked objects based on configurable rules.

## Scope
Minimal prototype only: no authentication, no database, no fancy UI.
Prioritize a working end-to-end pipeline over polish.

## Input
- Camera definitions are in `./data/cameras.json` (camera name, stream URL, enabled flag).
- If the JSON schema is not defined yet, propose a simple one and document it.

## Architecture
- The .NET app reads camera streams, runs inference, annotates frames, and
  **publishes** the annotated streams to MediaMTX.
- The HTML client does **not** talk to the .NET app for video: it connects
  **directly to MediaMTX** to play the processed streams (WebRTC preferred,
  HLS as fallback).

## Pipeline
1. Connect to every camera listed in `cameras.json`.
2. Run YOLO26n inference (via YoloSharp) on the frames.
3. Draw annotations on each frame: bounding box, class label, confidence score, and tracking ID.
4. Publish each annotated stream to MediaMTX — one stream path per camera
   (e.g., `annotated/{cam_name}`).
5. Provide a minimal HTML client that connects directly to MediaMTX and displays
   the processed stream of each camera.

## MediaMTX via Docker Compose
- Provide a `docker-compose.yml` that runs MediaMTX with:
  - The required ports exposed (RTSP, WebRTC/HTTP, HLS).
  - A mounted `mediamtx.yml` configuration file with the stream paths used by the app.
- Include the `mediamtx.yml` in the repository and document any values that must
  be adjusted (host IP, ports).
- Starting the whole media layer must be a single command: `docker compose up -d`.

## Recording rules
- A configuration file defines which object classes must be tracked (e.g., `person`)
  and the limits (confidence threshold, max segment duration — default 1 minute).
- When a tracked class is detected:
  - Track that object (by tracking ID) until it leaves the frame **or** the segment
    reaches the max duration (default: 1 minute).
  - Save the segment as:
    `output/{yyyy-MM-dd}/{cam_name}/{class_name}_{HH-mm-ss}_to_{HH-mm-ss}.mp4`
  - If the object is still present when a segment closes, immediately start a new
    1-minute segment (producing multiple consecutive clips).
  - When the object finally leaves, also generate one merged video containing the
    entire track (all segments concatenated).

## Configuration
One config file (JSON or an `appsettings` section) with at least:
- Object classes to track
- Confidence threshold
- Max segment duration (default: 60 s)
- Output root folder
- MediaMTX publish URL (host/port)

## Technology
- .NET 10
- YoloSharp for YOLO26n inference
- MediaMTX as the media server (running in Docker via Docker Compose)
- Plain HTML/JS client (no SPA framework) connecting directly to MediaMTX
- Solution in the new `.slnx` format
- `Directory.Build.props` + `Directory.Packages.props` (central package management)

## Deliverables
- Running solution implementing the pipeline above
- `docker-compose.yml` + `mediamtx.yml` for the media server
- Sample `cameras.json` and tracking configuration file
- Short README: how to start MediaMTX (`docker compose up -d`), run the app,
  and open the HTML client