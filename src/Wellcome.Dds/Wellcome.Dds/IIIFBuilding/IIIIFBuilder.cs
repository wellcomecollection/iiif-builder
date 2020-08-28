using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Wellcome.Dds.IIIFBuilding
{
    public interface IIIIFBuilder
    {
        /// <summary>
        /// Builds the IIIF Manifest or Collection for the given identifier.
        /// This might just be one volume in a multi-volume work.
        /// </summary>
        /// <param name="identifier">e.g., b12345678, b87654321_0001</param>
        /// <returns></returns>
        public Task<BuildResult> Build(string identifier);
        
        /// <summary>
        /// Builds ALL Manifests and Collections for the given bNumber.
        /// For a single volume work this does the same as Build(..),
        /// but for a multi-volume work, it will 
        /// </summary>
        /// <param name="bNumber"></param>
        /// <returns></returns>
        public Task<BuildResult> BuildAllManifestations(string bNumber);
    }
}
