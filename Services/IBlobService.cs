using System.IO;
using System.Threading.Tasks;

namespace QuanLyCotWeb.Services
{
    public interface IBlobService
    {
        Task<string> UploadAsync(Stream fileStream, string fileName);
        Task DeleteAsync(string fileName);
    }
}
