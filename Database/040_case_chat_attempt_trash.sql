-- Trash organizes visible attempts without erasing immutable learning evidence.
-- Only the user message stores the state; its paired assistant inherits it when read.
ALTER TABLE case_analysis_messages
 ADD COLUMN trashed_at timestamptz,
 ADD CONSTRAINT case_chat_trash_user_role CHECK(trashed_at IS NULL OR role='user');
