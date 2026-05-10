namespace NepDateWidget.Services;

public interface ISearchHistoryService
{
    IReadOnlyList<string> GetMatching(string prefix, int max = 10);
    void Record(string term);
    void Load();
    void Save();
}
