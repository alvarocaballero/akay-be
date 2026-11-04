-- Review before applying. The legacy integer is retained without modification;
-- it is not a processing request identifier and must not be converted to one.
BEGIN;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.columns
               WHERE table_schema = 'academic'
                 AND table_name = 'topic_content'
                 AND column_name = 'processing_attempt_id') THEN
        ALTER TABLE academic.topic_content ALTER COLUMN processing_attempt_id DROP NOT NULL;
    END IF;
END $$;

ALTER TABLE academic.topic_content ADD COLUMN IF NOT EXISTS request_id uuid;
ALTER TABLE academic.topic_content ADD COLUMN IF NOT EXISTS created_by integer;
ALTER TABLE academic.topic_content ADD COLUMN IF NOT EXISTS updated_by integer;

COMMIT;
