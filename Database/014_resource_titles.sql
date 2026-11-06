ALTER TABLE course_resources ADD COLUMN display_title text NOT NULL DEFAULT '' CHECK(length(display_title)<=200);
ALTER TABLE course_resources ADD COLUMN title_version integer NOT NULL DEFAULT 1 CHECK(title_version>0);
