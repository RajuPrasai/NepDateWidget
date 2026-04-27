using NepDateWidget.Helpers;
using NepDateWidget.Models;
using NepDateWidget.Services;
using System.Collections.ObjectModel;
using System.Windows.Input;
using System.Windows.Threading;

namespace NepDateWidget.ViewModels;

public sealed class MoreViewModel : ViewModelBase
{
    private readonly ILocalizationService _loc;
    private readonly INotesService? _notesService;
    private readonly IReminderService? _reminderService;

    // ── Mode toggle (Notes | Reminders) ─────────────────────────────────────

    private bool _isModeNotes = true;
    public bool IsModeNotes
    {
        get => _isModeNotes;
        private set => SetProperty(ref _isModeNotes, value);
    }
    public bool IsModeReminders => !_isModeNotes;

    public ICommand SetModeNotesCommand { get; }
    public ICommand SetModeRemindersCommand { get; }

    // ── Notes ────────────────────────────────────────────────────────────────

    public ObservableCollection<NoteItemViewModel> Notes { get; } = new();

    private string? _editingNoteKey;
    private string _noteEditBuffer = string.Empty;
    public string NoteEditBuffer
    {
        get => _noteEditBuffer;
        set => SetProperty(ref _noteEditBuffer, value);
    }

    // ── Reminders ────────────────────────────────────────────────────────────

    public ObservableCollection<ReminderItemViewModel> Reminders { get; } = new();

    // ── Labels ───────────────────────────────────────────────────────────────

    public string NotesHeadingLabel { get; private set; } = string.Empty;
    public string RemindersHeadingLabel { get; private set; } = string.Empty;
    public string NoNotesLabel { get; private set; } = string.Empty;
    public string NoRemindersLabel { get; private set; } = string.Empty;
    public string NoNotesHintLabel { get; private set; } = string.Empty;
    public string NoRemindersHintLabel { get; private set; } = string.Empty;
    public string SaveLabel   { get; private set; } = string.Empty;
    public string CancelLabel { get; private set; } = string.Empty;
    public string DeleteLabel { get; private set; } = string.Empty;
    public string EditLabel   { get; private set; } = string.Empty;

    // ── Commands ─────────────────────────────────────────────────────────────

    public ICommand DeleteNoteCommand { get; }
    public ICommand StartEditNoteCommand { get; }
    public ICommand SaveNoteCommand { get; }
    public ICommand CancelNoteEditCommand { get; }
    public ICommand DeleteReminderCommand { get; }
    public ICommand EditReminderCommand { get; }

    /// <summary>Raised when the user wants to edit a reminder. Args: reminderId.</summary>
    public event Action<string>? EditReminderRequested;

    // ── Construction ─────────────────────────────────────────────────────────

    public MoreViewModel(
        ILocalizationService localizationService,
        INotesService? notesService = null,
        IReminderService? reminderService = null)
    {
        _loc = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _notesService = notesService;
        _reminderService = reminderService;

        DeleteNoteCommand = new RelayCommand<string>(DoDeleteNote);
        StartEditNoteCommand = new RelayCommand<string>(DoStartEditNote);
        SaveNoteCommand = new RelayCommand(DoSaveNote);
        CancelNoteEditCommand = new RelayCommand(DoCancelNoteEdit);
        DeleteReminderCommand = new RelayCommand<string>(DoDeleteReminder);
        EditReminderCommand = new RelayCommand<string>(DoEditReminder);
        SetModeNotesCommand = new RelayCommand(() => { IsModeNotes = true; OnPropertyChanged(nameof(IsModeReminders)); });
        SetModeRemindersCommand = new RelayCommand(() => { IsModeNotes = false; OnPropertyChanged(nameof(IsModeReminders)); });

        if (_notesService is not null)
            _notesService.NotesChanged += (_, _) => RefreshNotes();
        if (_reminderService is not null)
            _reminderService.RemindersChanged += (_, _) => RefreshReminders();

        RefreshLabels();
        RefreshNotes();
        RefreshReminders();
    }

    public void OnLanguageChanged()
    {
        RefreshLabels();
        RefreshNotes();
        RefreshReminders();
    }

    /// <summary>
    /// Switches to Reminders mode and temporarily highlights the given reminder.
    /// Called when the user clicks a notification popup.
    /// </summary>
    public void NavigateToReminder(string reminderId)
    {
        RefreshReminders(); // Ensure data is current regardless of how we were called
        IsModeNotes = false;
        OnPropertyChanged(nameof(IsModeReminders));

        foreach (var r in Reminders)
            r.IsHighlighted = r.Id == reminderId;

        // Clear highlight after 3 seconds
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            foreach (var r in Reminders)
                r.IsHighlighted = false;
        };
        timer.Start();
    }

    // ── Refresh data ─────────────────────────────────────────────────────────

    private void RefreshLabels()
    {
        NotesHeadingLabel = _loc.Get("more.notes_heading");
        RemindersHeadingLabel = _loc.Get("more.reminders_heading");
        NoNotesLabel = _loc.Get("more.no_notes");
        NoRemindersLabel = _loc.Get("more.no_reminders");
        NoNotesHintLabel = _loc.Get("more.no_notes_hint");
        NoRemindersHintLabel = _loc.Get("more.no_reminders_hint");
        SaveLabel   = _loc.Get("more.save");
        CancelLabel = _loc.Get("more.cancel");
        DeleteLabel = _loc.Get("more.delete");
        EditLabel   = _loc.Get("more.edit");

        OnPropertyChanged(nameof(NotesHeadingLabel));
        OnPropertyChanged(nameof(RemindersHeadingLabel));
        OnPropertyChanged(nameof(NoNotesLabel));
        OnPropertyChanged(nameof(NoRemindersLabel));
        OnPropertyChanged(nameof(NoNotesHintLabel));
        OnPropertyChanged(nameof(NoRemindersHintLabel));
        OnPropertyChanged(nameof(SaveLabel));
        OnPropertyChanged(nameof(CancelLabel));
        OnPropertyChanged(nameof(DeleteLabel));
        OnPropertyChanged(nameof(EditLabel));
    }

    public void RefreshNotes()
    {
        Notes.Clear();
        _editingNoteKey = null;
        if (_notesService is null) return;
        foreach (var (key, value) in _notesService.GetAll().OrderByDescending(kv => kv.Key))
        {
            Notes.Add(new NoteItemViewModel(key, value));
        }
        OnPropertyChanged(nameof(HasNotes));
    }

    public void RefreshReminders()
    {
        Reminders.Clear();
        if (_reminderService is null) return;
        foreach (var r in _reminderService.GetAll().OrderByDescending(r => r.BsDate))
        {
            Reminders.Add(new ReminderItemViewModel(r));
        }
        OnPropertyChanged(nameof(HasReminders));
    }

    public bool HasNotes => Notes.Count > 0;
    public bool HasReminders => Reminders.Count > 0;

    // ── Note operations ──────────────────────────────────────────────────────

    private void DoDeleteNote(string? dateKey)
    {
        if (dateKey is null || _notesService is null) return;
        _notesService.DeleteNote(dateKey);
        RefreshNotes();
    }

    private void DoStartEditNote(string? dateKey)
    {
        if (dateKey is null) return;
        DoCancelNoteEdit();
        _editingNoteKey = dateKey;
        var item = Notes.FirstOrDefault(n => n.DateKey == dateKey);
        if (item is not null)
        {
            NoteEditBuffer = item.Text;
            item.IsEditing = true;
        }
    }

    private void DoSaveNote()
    {
        if (_editingNoteKey is null || _notesService is null) return;
        _notesService.SetNote(_editingNoteKey, NoteEditBuffer);
        _editingNoteKey = null;
        NoteEditBuffer = string.Empty;
        RefreshNotes();
    }

    private void DoCancelNoteEdit()
    {
        if (_editingNoteKey is null) return;
        var item = Notes.FirstOrDefault(n => n.DateKey == _editingNoteKey);
        if (item is not null) item.IsEditing = false;
        _editingNoteKey = null;
        NoteEditBuffer = string.Empty;
    }

    // ── Reminder operations ──────────────────────────────────────────────────

    private void DoDeleteReminder(string? id)
    {
        if (id is null || _reminderService is null) return;
        _reminderService.Delete(id);
        RefreshReminders();
    }

    private void DoEditReminder(string? id)
    {
        if (id is not null)
            EditReminderRequested?.Invoke(id);
    }
}

// ── Item ViewModels ──────────────────────────────────────────────────────────

public sealed class NoteItemViewModel : ViewModelBase
{
    public string DateKey { get; }
    public string Text { get; }
    public string DisplayDate { get; }

    private bool _isEditing;
    public bool IsEditing
    {
        get => _isEditing;
        set
        {
            if (SetProperty(ref _isEditing, value))
                OnPropertyChanged(nameof(IsNotEditing));
        }
    }
    public bool IsNotEditing => !_isEditing;

    public NoteItemViewModel(string dateKey, string text)
    {
        DateKey = dateKey;
        Text = text;
        DisplayDate = dateKey; // "YYYY-MM-DD" BS date
    }
}

public sealed class ReminderItemViewModel : ViewModelBase
{
    public string Id { get; }
    public string Title { get; }
    public string Date { get; }
    public string Time { get; }
    public string Notes { get; }
    public bool HasNotes => !string.IsNullOrEmpty(Notes);
    public bool IsCompleted { get; }
    public string RecurrenceText { get; }

    private bool _isHighlighted;
    public bool IsHighlighted
    {
        get => _isHighlighted;
        set => SetProperty(ref _isHighlighted, value);
    }

    public ReminderItemViewModel(ReminderEntry entry)
    {
        Id = entry.Id;
        Title = entry.Title;
        Date = entry.BsDate;
        Time = entry.Time;
        Notes = entry.Notes;
        IsCompleted = entry.IsCompleted;
        RecurrenceText = entry.Recurrence switch
        {
            ReminderRecurrence.Daily => "Daily",
            ReminderRecurrence.Weekly => "Weekly",
            ReminderRecurrence.Monthly => "Monthly",
            _ => string.Empty,
        };
    }
}
