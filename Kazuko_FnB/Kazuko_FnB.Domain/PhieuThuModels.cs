using System.Collections.Generic;

namespace Kazuko_FnB.Domain
{
    /// <summary>Ô nào được bật theo "Loại đối tượng" của phiếu thu.</summary>
    public sealed class ObjectFieldState
    {
        public bool Name { get; set; }
        public bool Address { get; set; }
        public bool Employee { get; set; }
        public bool Customer { get; set; }
        public bool Supplier { get; set; }
    }
}
