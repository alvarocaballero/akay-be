-- Review existing requested_by_user_id values and in-flight requests before applying.
-- Conversion notifications now use created_by, the original uploader.
ALTER TABLE academic.topic_content DROP COLUMN IF EXISTS requested_by_user_id;
