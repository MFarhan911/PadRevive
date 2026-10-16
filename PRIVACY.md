# Privacy

PadRevive keeps controller profiles, settings, game rules, and optional diagnostic records locally under `%LOCALAPPDATA%/GenericPadBridge`. It does not require an account and has no telemetry or automatic diagnostic upload service.

Revive AI's drift learner, profile insights and latency monitor work locally. The optional AI adviser uses your OpenAI API account. Only after you connect, select summary sharing and choose **Ask Revive AI** does the app send an aggregate measurement summary and relevant controller settings to `api.openai.com`. Device paths, serials, game paths, profile names, photos, audio and the full input trace are excluded. The adviser returns suggestions; it cannot change controller settings or run commands.

An API key stays in memory for the current session unless you choose to remember it in Windows Credential Manager for your Windows account. It is not saved in profiles, diagnostic exports or the source code. **Forget connection** removes PadRevive's saved credential. AI requests disable API response storage; OpenAI still processes requests under its [API data policy](https://developers.openai.com/api/docs/guides/your-data).

Optional browser searches send a controller hardware ID or generic controller description to the selected website. Imported controller photos are re-encoded without image metadata.

Review diagnostic exports before sharing: some reports can contain device paths or selected game paths. Input trace exports omit device paths and replace profile names with generic labels.

The public website contains no advertising or analytics scripts. Its hosting provider receives ordinary website requests.
