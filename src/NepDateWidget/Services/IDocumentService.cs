using NepDateWidget.Models;

namespace NepDateWidget.Services;

public interface IDocumentService
{
    IReadOnlyList<DocumentEntry> GetAll();
    void Add(DocumentEntry entry);
    void Update(DocumentEntry entry);
    void Delete(string id);
    void Load();
    void Save();
    event EventHandler? DocumentsChanged;
}
