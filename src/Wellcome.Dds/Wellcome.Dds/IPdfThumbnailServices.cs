using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using IIIF;

namespace Wellcome.Dds
{
    public interface IPdfThumbnailServices
    {
        Task<List<Size>> EnsurePdfThumbnails(Func<Task<Stream>> pdfStreamSource, int[] thumbSizes, string identifier);
    }
}