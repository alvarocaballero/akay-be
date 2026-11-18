-- Apply manually before enabling conversion processing.
-- Existing attempts without a recorded requester are intentionally not correlated.
ALTER TABLE academic.topic_content
    ADD COLUMN IF NOT EXISTS requested_by_user_id integer NULL;
