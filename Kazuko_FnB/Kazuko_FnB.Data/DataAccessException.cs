using System;

namespace Kazuko_FnB.Data
{
    /// <summary>
    /// Lỗi truy cập dữ liệu do lớp Data ném ra. Bọc FbException để UI/Services không phải bắt kiểu Firebird.
    /// Message giữ nguyên message gốc của Firebird; lỗi gốc nằm ở InnerException.
    /// </summary>
    public class DataAccessException : Exception
    {
        public DataAccessException(string message, Exception inner)
            : base(message, inner)
        {
        }

        public static DataAccessException From(Exception ex)
        {
            return new DataAccessException(ex.Message, ex);
        }
    }
}
