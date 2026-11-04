-- Review and execute against the target database before deploying the upload feature.
BEGIN;

-- Existing unfinished uploads cannot be resumed without the original file.
UPDATE academic.topic_content
SET processing_state = 2,
    processing_error = 'Upload interrupted. Delete the content and upload the file again.'
WHERE processing_state IN (0, 1) AND deleted_at IS NULL;

COMMIT;
