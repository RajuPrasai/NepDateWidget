namespace NepDateWidget.Services;

public interface INotesService
{
    string? GetNote(string dateKey);
    void SetNote(string dateKey, string? text);
    void DeleteNote(string dateKey);
    IReadOnlyDictionary<string, string> GetAll();
    HashSet<int> GetHasNotesForMonth(int bsYear, int bsMonth);
    void Load();
    void Save();
    event EventHandler? NotesChanged;
}
