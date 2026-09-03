# SPEC-19 — Voice commands: WhatsApp audio → Whisper → the text agent

Extends SPEC-17/18: a contact can *speak* a command as a WhatsApp voice note. The audio
is transcribed by a local Whisper server and then handled exactly like a text message;
the reply stays text and starts by quoting what was understood.

## Problem

Voice notes were dropped at the webhook ("Mensagem sem texto"). The owner wants to say
"ativar os alertas", "status" or "últimas capturas de pessoas" instead of typing, without
sending the audio to a third party.

## Flow

```
voice note → Evolution webhook (webhookBase64: true → data.message.audioMessage + base64)
  → POST /api/whatsapp/webhook stores data/inbound-audio/{yyyyMMdd}/{id}.ogg + a row (Kind = Audio, Text = "")
  → WhatsAppCommandHostedService: ISpeechToTextClient (Whisper) → row.Text = transcript
  → classify / act as text → reply "🎤 Entendi: \"…\"" + blank line + the normal answer
```

## Whisper server (docker-compose)

- `whisper` (default): `hwdsl2/whisper-server:cuda`, `WHISPER_MODEL=large-v3-turbo`,
  `WHISPER_LANGUAGE=pt`, `WHISPER_DEVICE=cuda`, port 9000, volume `whisper_data`, NVIDIA GPU
  reservation (needs the NVIDIA Container Toolkit / Docker Desktop WSL2 GPU support).
- `whisper-cpu` (profile `whisper-cpu`): `hwdsl2/whisper-server:latest`, `WHISPER_MODEL=small`,
  `WHISPER_DEVICE=cpu`, same port and volume. Exactly one runs at a time:
  `docker compose stop whisper && docker compose --profile whisper-cpu up -d whisper-cpu`.
- `WHISPER_API_KEY=cameravision-whisper-key` is set explicitly (fresh persistent installs would
  otherwise auto-generate a key) and must match *Chave* on `/system/ai`.
- API: OpenAI-compatible `POST /v1/audio/transcriptions` (multipart `file`, `model=whisper-1`,
  `language`, `response_format=json`) → `{"text": "..."}`.

## Inbound audio

- The webhook is registered with `webhookBase64: true`, so voice notes arrive decoded in
  `data.message.base64` — `POST /chat/getBase64FromMediaMessage` is broken for iOS voice notes on
  Evolution 2.3.x (issue #2550) and is used only as a fallback when the base64 is missing.
  Existing installs must click **Registrar webhook** again; the chip says *Webhook sem áudio —
  registre novamente* until they do (`EvolutionWebhookState.Base64`).
- `WhatsAppInboundMessage.TryParse` accepts `audioMessage` (also inside `ephemeralMessage`):
  `Kind = Audio`, `AudioBase64`, `AudioMimeType` (`audio/ogg; codecs=opus`), `AudioSeconds`.
  Images, videos, stickers and reactions are still rejected.
- The API writes the file under `StoragePaths.InboundAudioRoot` (`data/inbound-audio`, next to the
  database), at most 5 MB, and stores the row with `Kind`, `AudioPath`, `AudioMimeType`,
  `AudioSeconds` and an empty `Text`.

## Processing (`WhatsAppCommandHostedService`)

After the contact and antiflood checks, an `Audio` row goes through `TranscribeAsync`:
audio commands disabled → "Comandos por áudio estão desativados…" (Done); longer than
`WhatsAppAudioMaxSeconds` (default 60) → "Áudio muito longo (máximo de N s)…" (Done); file missing →
`GetMediaBase64Async` fallback; Whisper failure or empty transcript → "Não consegui entender o
áudio…" (Failed, `Detail` = reason). On success `Text` = transcript, `Detail` = "Transcrito em N s",
the audio file is deleted (only the text is kept) and the message continues exactly as text;
the reply is prefixed with `CommandReplyText.Heard(transcript)`.

## Interface and settings

- `ISpeechToTextClient` (Core): `TranscribeAsync(SystemSettings, SpeechToTextRequest(Audio,
  MimeType, FileName, Language)) → SpeechToTextResult(Success, Text, Error)`; never throws.
  Implementation `WhisperSpeechToTextClient` (Infrastructure) — Bearer key when set, 60 s timeout,
  empty transcript is a success with empty `Text`.
- `SystemSettings`: `WhatsAppAudioEnabled` (true), `WhisperBaseUrl` (`http://localhost:9000`),
  `WhisperApiKey`, `WhisperLanguage` (`pt`, empty = autodetect), `WhatsAppAudioMaxSeconds` (60).
  Migration `WhatsAppAudioCommands`.
- `/system/ai`, section **Transcrição de áudio (Whisper)**: switch, URL, idioma, chave, duração
  máxima, **Testar Whisper** (sends one second of silence; green chip = the server answered).
- `/system/whatsapp` table: a microphone icon marks audio rows and *Mensagem* shows the transcript.

Tests: `WhatsAppInboundMessageTests` (inline audio, audio without base64, ephemeral audio,
image still rejected).
