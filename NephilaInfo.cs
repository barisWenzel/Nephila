using System;
using System.Drawing;
using Grasshopper.Kernel;

namespace Nephila
{
    /// <summary>
    /// Plugin-Metadaten – einmal pro Assembly.
    /// </summary>

    public class NephilaInfo : GH_AssemblyInfo
    {
        public override string Name => "Nephila";
        public override string Version => "0.1.0";
        public override string Description => "Structural form-finding tools: graph topology & force density method.";
        public override string AuthorName => "Baris Wenzel";
        public override string AuthorContact => "baris@bariss.de";
        public override Bitmap Icon => null; // eigenes 24×24 Bitmap hier

        public override GH_LibraryLicense License =>
            GH_LibraryLicense.opensource;
    }
}
