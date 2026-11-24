-- Expand capacity without reinterpreting or rewriting immutable historical responses.
-- Preserve nullable legacy metadata, assistant-only annotations and array validation.
ALTER TABLE case_analysis_messages
 DROP CONSTRAINT case_chat_annotations_role,
 ADD CONSTRAINT case_chat_annotations_role CHECK(annotations IS NULL OR
   (role='assistant' AND jsonb_typeof(annotations)='array' AND jsonb_array_length(annotations)<=128));
