ALTER TABLE course_resources
 ADD COLUMN resource_type text NOT NULL DEFAULT 'file' CHECK(resource_type IN ('file','youtube')),
 ADD COLUMN youtube_video_id text,
 ADD CONSTRAINT ck_resource_source CHECK(
  (resource_type='file' AND youtube_video_id IS NULL) OR
  (resource_type='youtube' AND youtube_video_id IS NOT NULL AND youtube_video_id ~ '^[A-Za-z0-9_-]{11}$' AND octet_length(content)=0)
 );
