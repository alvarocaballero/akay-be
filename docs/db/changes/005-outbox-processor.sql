-- Review and apply before enabling the Outbox processor on an existing database.
ALTER TABLE infra.outbox_message
    ADD COLUMN IF NOT EXISTS attempts integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS next_attempt_at timestamp with time zone NULL,
    ADD COLUMN IF NOT EXISTS lease_owner character varying(128) NULL,
    ADD COLUMN IF NOT EXISTS lease_expires_at timestamp with time zone NULL,
    ADD COLUMN IF NOT EXISTS failed_at timestamp with time zone NULL,
    ADD COLUMN IF NOT EXISTS not_deliverable_at timestamp with time zone NULL;

-- Existing indexes with these names must be checked for filter drift separately.
CREATE INDEX IF NOT EXISTS ix_outbox_message_pending
    ON infra.outbox_message (processed_at, failed_at, next_attempt_at)
    WHERE processed_at IS NULL AND failed_at IS NULL AND not_deliverable_at IS NULL;

CREATE INDEX IF NOT EXISTS ix_outbox_message_lease_expires_at
    ON infra.outbox_message (lease_expires_at)
    WHERE processed_at IS NULL AND failed_at IS NULL AND not_deliverable_at IS NULL AND lease_expires_at IS NOT NULL;
