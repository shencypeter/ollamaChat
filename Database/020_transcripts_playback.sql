ALTER TABLE course_resources
 ADD COLUMN transcript text NOT NULL DEFAULT '' CHECK(length(transcript)<=100000),
 ADD COLUMN transcript_version integer NOT NULL DEFAULT 1 CHECK(transcript_version>0);
ALTER TABLE learning_time_sessions
 ADD COLUMN playback_rate double precision NOT NULL DEFAULT 1 CHECK(playback_rate BETWEEN 0.25 AND 4),
 ADD COLUMN content_milliseconds bigint NOT NULL DEFAULT 0 CHECK(content_milliseconds>=0);
-- Existing credited video time was restricted to 1x. Keep its original meaning.
UPDATE learning_time_sessions SET content_milliseconds=credited_milliseconds;
