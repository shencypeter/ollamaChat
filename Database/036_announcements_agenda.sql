CREATE TABLE course_announcements (
 id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
 course_id uuid NOT NULL REFERENCES courses(id),
 title text NOT NULL CHECK(length(btrim(title)) BETWEEN 1 AND 200),
 body text NOT NULL CHECK(length(btrim(body)) BETWEEN 1 AND 16000),
 status text NOT NULL DEFAULT 'draft' CHECK(status IN ('draft','published')),
 author_id uuid NOT NULL REFERENCES app_users(id),
 version integer NOT NULL DEFAULT 1 CHECK(version > 0),
 created_at timestamptz NOT NULL DEFAULT now(),
 updated_at timestamptz NOT NULL DEFAULT now(),
 published_at timestamptz,
 CHECK(status <> 'published' OR published_at IS NOT NULL)
);
CREATE INDEX ix_announcements_course_published ON course_announcements(course_id,published_at DESC,id) WHERE status='published';
ALTER TABLE course_grade_items ADD COLUMN scheduled_at timestamptz;
CREATE INDEX ix_grade_items_schedule ON course_grade_items(course_id,scheduled_at) WHERE scheduled_at IS NOT NULL;

-- Legacy rows have no separate publication clock. Preserve their last recorded
-- update as an approximate historical publication time; future changes are exact.
ALTER TABLE course_grade_scores ADD COLUMN published_at timestamptz;
UPDATE course_grade_scores SET published_at=updated_at WHERE published_score IS NOT NULL;
CREATE FUNCTION maintain_grade_score_publication_time() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF TG_OP='INSERT' THEN
  NEW.published_at := CASE WHEN NEW.published_score IS NULL THEN NULL ELSE now() END;
 ELSIF NEW.published_score IS DISTINCT FROM OLD.published_score THEN
  NEW.published_at := CASE WHEN NEW.published_score IS NULL THEN NULL ELSE now() END;
 ELSE
  NEW.published_at := OLD.published_at;
 END IF;
 RETURN NEW;
END;
$$;
CREATE TRIGGER trg_grade_score_publication_time BEFORE INSERT OR UPDATE ON course_grade_scores
 FOR EACH ROW EXECUTE FUNCTION maintain_grade_score_publication_time();
