namespace Wellcome.Dds.Identifiers
{
    /// <summary>
    /// Source system names for the Identifiers API. The registry is written by the catalogue pipeline's
    /// ID Minter, so its SourceSystem column uses the pipeline's identifier-type vocabulary:
    /// https://github.com/wellcomecollection/catalogue-pipeline/blob/main/common/internal_model/src/main/scala/weco/catalogue/internal_model/identifiers/IdentifierType.scala
    ///
    /// Every constant here is defined in that file, but that does not mean the registry holds rows
    /// for it. As of 2026-10-07, only sierra-system-number, mets, calm-record-id, axiell-guid and
    /// miro-image-number have been confirmed by a 200 from the live API. The RFC states that
    /// folio-instance rows exist and folio-item rows do not yet. The rest are unverified.
    ///
    /// This is an open set, not a contract: the API accepts any value and returns 404 for one it does not know.
    /// </summary>
    public static class SourceSystems
    {
        public const string SierraSystemNumber = "sierra-system-number";
        public const string SierraIdentifier = "sierra-identifier";
        public const string Mets = "mets"; // Is this always the Preservation ID?
        public const string CalmRecordId = "calm-record-id";
        public const string CalmRefNo = "calm-ref-no";
        public const string CalmAltRefNo = "calm-altref-no";
        public const string MiroImageNumber = "miro-image-number";
        public const string AxiellGuid = "axiell-guid";
        public const string FolioInstance = "folio-instance";
        public const string FolioInstanceHrid = "folio-instance-hrid";
        public const string FolioItem = "folio-item";

        public static readonly string[] Known =
        {
            SierraSystemNumber,
            SierraIdentifier,
            Mets,
            CalmRecordId,
            CalmRefNo,
            CalmAltRefNo,
            MiroImageNumber,
            AxiellGuid,
            FolioInstance,
            FolioInstanceHrid,
            FolioItem
        };
    }
}
