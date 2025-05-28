using System.IO;
using System.Threading.Tasks;

public static class FileReader
{
    public static async Task<string> ReadTextAsync(string category, string fileName)
    {
        string path = Path.Combine("Base_Text", category, fileName + ".txt");

        if (!File.Exists(path))
            throw new FileNotFoundException("Файл не найден", path);

        return await File.ReadAllTextAsync(path);
    }
}
