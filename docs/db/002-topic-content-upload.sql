-- Review and execute against the target database before deploying the upload feature.
BEGIN;

ALTER TABLE academic.topic_content
    ADD COLUMN IF NOT EXISTS upload_started_at timestamp with time zone NULL,
    ADD COLUMN IF NOT EXISTS upload_user_id integer NULL;

-- Existing unfinished uploads cannot be resumed without the original file.
UPDATE academic.topic_content
SET processing_state = 2,
    processing_error = 'Upload interrupted. Please retry.'
WHERE processing_state IN (0, 1) AND deleted_at IS NULL;

COMMIT;
