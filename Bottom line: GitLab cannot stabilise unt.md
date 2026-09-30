Bottom line: GitLab cannot stabilise until the host issue is fixed. The last probes show it cycling 000 → 502 → 000 — each restart needs 2–4 minutes to boot, but the heartbeat failures reassign tasks every ~5 minutes, so it never finishes. It's not a config problem; it's being knocked over faster than it can come up.

One thing that concerns me: stop_grace_period is 300 s, and GitLab's log showed timeout: run: postgresql: (pid 1141) 406s, want down — Postgres wanting longer than the grace period, then getting force-killed. Repeated hard kills of a running Postgres is how you corrupt a database. There's nothing valuable in it yet, so the risk is currently low, but I'd rather not leave it crash-looping. Say the word and I'll scale the service to 0 replicas until the host is sorted, then scale it back — the volumes and config all persist.

---

What needs to be filled in

A. Blocking — GitLab is LAN-only until these are set (Portainer → Stacks → gitlab → Environment variables → Update the stack)

1. GITLAB_EXTERNAL_URL → https://gitlab.<yourdomain> (currently empty → falls back to http://192.168.0.25:8929)
2. REGISTRY_EXTERNAL_URL → https://registry.<yourdomain> (→ http://192.168.0.25:5050)
3. Two routes on your server-level tunnel: gitlab.<domain> → localhost:8929, registry.<domain> → localhost:5050

B. Credentials

4. gitlab_smtp_password — Docker secret currently holding REPLACE_ME. Immutable, so: docker secret rm + create + redeploy.
5. Root password — not a placeholder, GitLab generated it. root / SMimBmJodD9ufuwY0ShtKqtibeImY5zx0dB4pBmnAX8= — the source file self-deletes after 24 h, so change it at first login.

C. Email — all empty; SMTP stays fully off until SMTP_ADDRESS is set, by design

6. SMTP_ADDRESS · 7. SMTP_PORT (587) · 8. SMTP_USER_NAME · 9. SMTP_DOMAIN · 10. SMTP_AUTHENTICATION/SMTP_STARTTLS/SMTP_TLS if your relay differs · 11. EMAIL_FROM · 12. EMAIL_REPLY_TO

D. Optional off-site backup — local backups already work without this

13. BACKUP_S3_BUCKET · 14. BACKUP_S3_REGION · 15. BACKUP_S3_ENDPOINT · 16–17. the two gitlab_backup_s3_* secrets (also REPLACE_ME)

E. Host-level, outside the stack

18. Backup cron — omnibus has no scheduler. Add: 0 2 * * * docker exec -t $(docker ps -qf name=gitlab_gitlab) gitlab-backup create CRON=1. Note gitlab-backup does not include /etc/gitlab/gitlab-secrets.json — copy it separately or your backups are undecryp
19. TIME_ZONE if not UTC.                                                                                     20. Disable open sign-ups — currently allowcation setting, not a gitlab.rb key, so itmust be done in Admin → Settings → General.                                                               
Known caveats: git-over-SSH won't traverse an HTTP tunnel (port 2222 is LAN-only); docker push through a free Cloudflare plan hits the 100 MB body cap; nocker socket); and reconfigure flaggeddeprecated nginx[...]/registry_nginx[...] keys that I left alone rather than guess the nested-hash replacement and break a working boot.

The stack file is at …/scratchpad/gitlab-st the canonical copy — tell me where you'dlike it kept permanently and I'll put it there.


To log in right now:

http://192.168.0.25:8929      ← http, not https
user:     root
password: SMimBmJodD9ufuwY0ShtKqtibeImY5zx0dB4pBmnAX8=

Two things to do on that first login: change the root password (the source file self-deletes 24 h after first boot), and turn off open sign-ups under Admin → Settings → General → Sign-up restrictions — allow_signup is currently true, and it's an application setting so it can't be baked into the stack.

The rest of the fill-in list stands as I sent it — the blocking items being GITLAB_EXTERNAL_URL and REGISTRY_EXTERNAL_URL plus the two tunnel routes, and remembering that cloudflared's service must be http://localhost:8929, not https://.

Worth keeping an eye on regardless: the br0 MAC/subnet flapping in the daemon log was real, and if the swarm heartbeat starts timing out again that's where I'd look.
