CREATE TABLE notification_reads (
 user_id uuid NOT NULL REFERENCES app_users(id),
 kind text NOT NULL CHECK(kind IN ('quiz','report','question')),
 item_id uuid NOT NULL,
 read_at timestamptz NOT NULL DEFAULT now(),
 PRIMARY KEY(user_id,kind,item_id)
);
