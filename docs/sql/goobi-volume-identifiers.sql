-- Remediation for workflow jobs keyed by volume identifiers (November 2026)
--
-- Goobi started sending manifestation-level identifiers (e.g. b30454785_0001) in workflow
-- messages instead of the package-level b number. Before fix/goobi-volume-identifier, the
-- workflow processor recorded those jobs under the volume id, and the runner looked up the
-- Catalogue API by that id, found nothing, and failed with
--     "No work available in Catalogue API; highest access condition is Open"
-- so no IIIF manifest was built. The images themselves were registered with DLCS correctly
-- (DLCS asset ids come from the METS, not the workflow job id), so only the IIIF is missing.
--
-- Run against the dds database, in order:
--   1. PREVIEW  - see what's affected. Read-only.
--   2. REQUEUE  - queue a package-level IIIF rebuild per affected b number. Safe to re-run;
--                 run again after the fix is deployed to catch stragglers.
--   3. CLEANUP  - delete the volume-keyed rows, once the rebuilds have finished and Goobi
--                 sends b numbers again. Only deletes rows whose package has since had a
--                 successful job, so it is safe to run repeatedly.
--
-- Set the start date in each statement (the 'since' CTE) to just before the Goobi change.
-- dlcs_ingest_jobs rows are already keyed by package + volume part and need no changes.


-- ============================================================================================
-- 1. PREVIEW
-- ============================================================================================

WITH since AS (SELECT timestamp '2026-10-01' AS d)
SELECT substring(wj.identifier from '^(b\d{7}[\dx])_') AS package,
       string_agg(wj.identifier, ', ' ORDER BY wj.identifier) AS volume_jobs,
       min(wj.created) AS first_seen,
       max(wj.created) AS last_seen,
       count(*) FILTER (WHERE wj.error IS NOT NULL) AS with_error
FROM workflow_jobs wj, since
WHERE wj.identifier ~ '^b\d{7}[\dx]_\d'
  AND wj.created >= since.d
GROUP BY 1
ORDER BY first_seen;


-- ============================================================================================
-- 2. REQUEUE - one package-level job per affected b number
--
-- workflow_options is a RunnerOptionsFlags value (Wellcome.Dds.Common.RunnerOptions):
--   1 RegisterImages, 2 RefreshFlatManifestations, 4 RebuildIIIF,
--   8 RebuildTextCaches, 16 RebuildAllAnnoPageCaches
-- 6 rebuilds IIIF and saves the catalogue metadata that the failed jobs never saved.
-- Use 30 to also build text/annotation caches (everything except DLCS registration).
--
-- Only packages with no b-number job newer than their latest volume-keyed row are queued,
-- so re-running picks up new stragglers without resetting jobs already requeued (or sent
-- correctly by Goobi). A requeued job that failed is not retried; re-run it from the dashboard.
-- An existing older b-number row is reset, including ingest_job_started, so check the
-- preview for packages that may still have a DLCS ingest in flight.
-- ============================================================================================

BEGIN;

WITH since AS (SELECT timestamp '2026-10-01' AS d),
affected AS (
    SELECT substring(wj.identifier from '^(b\d{7}[\dx])_') AS package,
           max(wj.created) AS last_volume_job
    FROM workflow_jobs wj, since
    WHERE wj.identifier ~ '^b\d{7}[\dx]_\d'
      AND wj.created >= since.d
    GROUP BY 1
)
INSERT INTO workflow_jobs (
    identifier, created, waiting, finished, taken, error, ingest_job_started,
    workflow_options, force_text_rebuild, flush_cache, expedite,
    annos_already_on_disk, annos_built, dlcs_job_count, expected_texts, first_dlcs_job_id,
    package_build_time, text_and_anno_build_time, text_pages, texts_already_on_disk,
    texts_built, time_spent_on_text_pages, total_time, words)
SELECT a.package,
       now(), true, false, NULL, NULL, NULL,
       6, false, true, false,
       0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0
FROM affected a
LEFT JOIN workflow_jobs p ON p.identifier = a.package
WHERE p.identifier IS NULL
   OR p.created IS NULL
   OR p.created < a.last_volume_job
ON CONFLICT (identifier) DO UPDATE SET
    created = now(), waiting = true, finished = false, taken = NULL, error = NULL,
    ingest_job_started = NULL, workflow_options = EXCLUDED.workflow_options,
    force_text_rebuild = false, flush_cache = true, expedite = false;

-- Check the row count matches the number of packages in the preview, then:
COMMIT;
-- (or ROLLBACK;)


-- ============================================================================================
-- 3. CLEANUP - delete volume-keyed rows superseded by a successful package-level job
--
-- Run once the requeued jobs have finished (check the dashboard's workflow errors page)
-- and Goobi has been fixed. Rows whose package has not yet had a successful job created
-- after them are left alone, so anything still outstanding stays visible.
-- ============================================================================================

-- Preview what would be deleted, and what would be kept
WITH since AS (SELECT timestamp '2026-10-01' AS d)
SELECT v.identifier, v.created, v.error,
       p.identifier AS package_job, p.created AS package_job_created,
       (p.finished AND p.error IS NULL AND p.created > v.created) AS will_delete
FROM workflow_jobs v
CROSS JOIN since
LEFT JOIN workflow_jobs p ON p.identifier = substring(v.identifier from '^(b\d{7}[\dx])_')
WHERE v.identifier ~ '^b\d{7}[\dx]_\d'
  AND v.created >= since.d
ORDER BY will_delete NULLS FIRST, v.identifier;

BEGIN;

WITH since AS (SELECT timestamp '2026-10-01' AS d)
DELETE FROM workflow_jobs v
USING workflow_jobs p, since
WHERE v.identifier ~ '^b\d{7}[\dx]_\d'
  AND v.created >= since.d
  AND p.identifier = substring(v.identifier from '^(b\d{7}[\dx])_')
  AND p.finished
  AND p.error IS NULL
  AND p.created > v.created;

-- Check the row count matches will_delete = true above, then:
COMMIT;
-- (or ROLLBACK;)
