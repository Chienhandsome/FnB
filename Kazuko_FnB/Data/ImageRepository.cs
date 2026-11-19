using System;
using System.Collections.Generic;
using System.Data;
using FirebirdSql.Data.FirebirdClient;

namespace Kazuko_FnB.Data
{
    public sealed class ImageRecord
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public byte[] Data { get; set; }
    }

    /// <summary>Thư viện ảnh dùng chung (SIMAGE). Các bảng khác trỏ tới bằng cột SIMAGEID.</summary>
    public sealed class ImageRepository
    {
        private readonly string _databasePath;

        public ImageRepository(string databasePath)
        {
            _databasePath = databasePath;
        }

        /// <summary>Mọi ảnh, theo thứ tự tạo (ảnh cùng tên đứng cạnh nhau).</summary>
        public List<ImageRecord> LoadAll()
        {
            var list = new List<ImageRecord>();
            using (FbConnection conn = KazukoConnection.Open(_databasePath))
            {
                DataTable dt = DbHelper.Table(conn, null,
                    "SELECT ID, NAME, IMAGE FROM SIMAGE WHERE STATUS = 30 ORDER BY TIMECREATED, NAME");
                foreach (DataRow r in dt.Rows)
                    list.Add(new ImageRecord { Id = DbHelper.Str(r[0]), Name = DbHelper.Str(r[1]) ?? "", Data = r[2] as byte[] });
            }
            return list;
        }

        public byte[] Get(string id)
        {
            if (string.IsNullOrEmpty(id))
                return null;
            using (FbConnection conn = KazukoConnection.Open(_databasePath))
                return DbHelper.Scalar(conn, null, "SELECT IMAGE FROM SIMAGE WHERE ID = @I", "@I", id) as byte[];
        }

        public string Add(string name, byte[] data, string userId)
        {
            string id = DbHelper.NewId();
            DbHelper.InTransaction(_databasePath, delegate (FbConnection conn, FbTransaction tx)
            {
                DbHelper.Exec(conn, tx,
                    "INSERT INTO SIMAGE (ID, NAME, STATUS, USERCREATEDID, TIMECREATED, IMAGE) " +
                    "VALUES (@I, @N, 30, @U, @T, @D)",
                    "@I", id, "@N", name, "@U", userId, "@T", DbHelper.Now(), "@D", data);
            });
            return id;
        }

        public void Replace(string id, byte[] data, string userId)
        {
            DbHelper.InTransaction(_databasePath, delegate (FbConnection conn, FbTransaction tx)
            {
                DbHelper.Exec(conn, tx,
                    "UPDATE SIMAGE SET IMAGE = @D, USERMODIFIEDID = @U, TIMEMODIFIED = @T WHERE ID = @I",
                    "@D", data, "@U", userId, "@T", DbHelper.Now(), "@I", id);
            });
        }

        public void Rename(string id, string name, string userId)
        {
            DbHelper.InTransaction(_databasePath, delegate (FbConnection conn, FbTransaction tx)
            {
                DbHelper.Exec(conn, tx,
                    "UPDATE SIMAGE SET NAME = @N, USERMODIFIEDID = @U, TIMEMODIFIED = @T WHERE ID = @I",
                    "@N", name, "@U", userId, "@T", DbHelper.Now(), "@I", id);
            });
        }

        /// <summary>Số bản ghi (ở mọi bảng) còn dùng ảnh này.</summary>
        public int CountUsage(string id)
        {
            using (FbConnection conn = KazukoConnection.Open(_databasePath))
                return ReferenceChecker.Count(conn, null, "SIMAGEID", id);
        }

        /// <summary>Xóa hẳn dòng ảnh. Người gọi phải kiểm tra CountUsage trước.</summary>
        public void Delete(string id)
        {
            DbHelper.InTransaction(_databasePath, delegate (FbConnection conn, FbTransaction tx)
            {
                DbHelper.Exec(conn, tx, "DELETE FROM SIMAGE WHERE ID = @I", "@I", id);
            });
        }
    }
}
