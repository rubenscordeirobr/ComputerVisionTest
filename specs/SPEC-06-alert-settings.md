# SPEC-06 — Alert settings (configuration only)

## Objective

Configuration UI on `/alerts` for the two alert channels (Email and WhatsApp):
enable/disable, recipient list, and trigger classes — persisted per channel.
No sending logic.

## Scope

- Per-channel configuration bound to the two seeded `AlertSettings` rows.
- Recipient validation per channel (email address vs. phone number).

## Out of scope

- Dispatching alerts (SPEC-09 implements the dispatcher and the Email
  channel; WhatsApp sending remains future work).

## Dependencies

- SPEC-01 (AlertSettings entity/repository, `DetectableClasses`), SPEC-02
  (page shell).

## Tasks

- [ ] `/alerts` page with `MudTabs`: **E-mail** and **WhatsApp**; both tabs
      render a shared form component parameterized by `AlertChannel`.
- [ ] Form per channel:
  - `MudSwitch` **Ativado**;
  - **Destinatários**: `MudTextField` + **Adicionar** button (Enter also
    adds) → recipients rendered as closable `MudChip`s;
    - Email channel: validate with `MailAddress.TryCreate`; message
      "E-mail inválido.";
    - WhatsApp channel: normalize (strip spaces, dashes, parentheses) and
      require optional `+` followed by 10–15 digits; message
      "Número inválido. Use o formato +5549999999999.";
    - reject duplicates (case-insensitive) with "Destinatário já
      adicionado.";
  - **Classes que disparam alerta**: multi `MudSelect` over
    `DetectableClasses` (PT-BR labels, stores English names);
  - **Salvar** per tab → `ISettingsRepository.SaveAlertSettingsAsync` →
    snackbar "Configurações de alerta salvas.".
- [ ] Per-channel `MudAlert` (Info, PT-BR): **E-mail** tab — "Alertas por
      e-mail são enviados automaticamente quando uma nova captura corresponde
      às regras (requer SMTP configurado em Sistema)."; **WhatsApp** tab —
      "O envio por WhatsApp será implementado em uma versão futura — esta
      tela apenas armazena a configuração."

## Acceptance criteria

- Email and WhatsApp configurations save independently and survive restarts.
- Invalid email / invalid phone / duplicate recipient are rejected with the
  PT-BR messages above; valid ones become chips and persist.
- Trigger classes persist as English COCO names in the DB while the UI shows
  PT-BR labels.
- This spec adds no sending code (dispatch arrives in SPEC-09); build green.

## Changelog

- 2026-08-29 — Alerts refactor: Email sending is now implemented in v1
  (SPEC-09), so the "future version" info alert applies to WhatsApp only;
  the Email tab explains when emails are actually sent. The configuration
  model and forms are unchanged.
- 2026-08-29 — v2 refactor: `TriggerClasses` removed — what triggers an alert
  is now decided per capture rule (SPEC-10), so each channel tab keeps only
  the master switch + recipients (with a hint pointing to Regras de Captura).
  The page gains a third tab, **Saúde das câmeras**, holding the SPEC-13
  health-alert and anti-flood settings.
