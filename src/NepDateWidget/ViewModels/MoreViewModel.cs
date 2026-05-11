using NepDateWidget.Helpers;
using NepDateWidget.Models;
using NepDateWidget.Services;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Input;
using System.Windows.Threading;

namespace NepDateWidget.ViewModels;

public sealed class MoreViewModel : ViewModelBase
{
    private readonly ILocalizationService _loc;
    private readonly INotesService? _notesService;
    private readonly IReminderService? _reminderService;
    private readonly IDocumentService? _documentService;

    // ── Mode toggle (Documents | Notes | Reminders) ──────────────────────────

    private int _modeIndex = 1; // default to Notes tab
    public bool IsModeDocuments => _modeIndex == 0;
    public bool IsModeNotes     => _modeIndex == 1;
    public bool IsModeReminders => _modeIndex == 2;

    private void SetMode(int index)
    {
        if (_modeIndex == index) return;
        if (_modeIndex == 0) DoCancelDocumentEdit();
        if (_modeIndex == 1) DoCancelNoteForm();
        if (_modeIndex == 2) DoCancelReminderForm();
        _modeIndex = index;
        OnPropertyChanged(nameof(IsModeDocuments));
        OnPropertyChanged(nameof(IsModeNotes));
        OnPropertyChanged(nameof(IsModeReminders));
    }

    public ICommand SetModeDocumentsCommand { get; }
    public ICommand SetModeNotesCommand { get; }
    public ICommand SetModeRemindersCommand { get; }

    // ── Notes ────────────────────────────────────────────────────────────────

    public ObservableCollection<NoteItemViewModel> Notes { get; } = new();

    // ── Note inline form ─────────────────────────────────────────────────────

    private bool _isNoteFormOpen;
    public bool IsNoteFormOpen
    {
        get => _isNoteFormOpen;
        private set
        {
            if (SetProperty(ref _isNoteFormOpen, value))
            {
                OnPropertyChanged(nameof(ShowNoteEmpty));
                OnPropertyChanged(nameof(ShowNoteNoResults));
                OnPropertyChanged(nameof(NoteFormTitleLabel));
            }
        }
    }

    private string? _editingNoteKey;

    private string _noteFormDateInput = string.Empty;
    public string NoteFormDateInput
    {
        get => _noteFormDateInput;
        set
        {
            if (SetProperty(ref _noteFormDateInput, value))
                NoteFormError = string.Empty;
        }
    }

    private string _noteFormText = string.Empty;
    public string NoteFormText
    {
        get => _noteFormText;
        set => SetProperty(ref _noteFormText, value);
    }

    private string _noteFormError = string.Empty;
    public string NoteFormError
    {
        get => _noteFormError;
        private set
        {
            if (SetProperty(ref _noteFormError, value))
                OnPropertyChanged(nameof(HasNoteFormError));
        }
    }
    public bool HasNoteFormError => !string.IsNullOrEmpty(_noteFormError);

    public string NoteFormTitleLabel => _editingNoteKey is null
        ? _loc.Get("notes.form_title_add")
        : _loc.Get("notes.form_title_edit");

    // ── Note search ───────────────────────────────────────────────────────────

    // inline note edit buffer (used when editing from the list card directly)
    private string? _inlineEditingNoteKey;
    private string _noteEditBuffer = string.Empty;
    public string NoteEditBuffer
    {
        get => _noteEditBuffer;
        set => SetProperty(ref _noteEditBuffer, value?.Length > 500 ? value[..500] : value ?? string.Empty);
    }

    private string _noteSearchText = string.Empty;
    public string NoteSearchText
    {
        get => _noteSearchText;
        set
        {
            if (SetProperty(ref _noteSearchText, value))
            {
                OnPropertyChanged(nameof(IsNoteSearchActive));
                OnPropertyChanged(nameof(NoteSearchClearVisible));
                UpdateFilteredNotes();
            }
        }
    }
    public bool IsNoteSearchActive    => !string.IsNullOrWhiteSpace(_noteSearchText);
    public bool NoteSearchClearVisible => IsNoteSearchActive;

    private IReadOnlyList<NoteItemViewModel> _filteredNotes = Array.Empty<NoteItemViewModel>();
    public  IReadOnlyList<NoteItemViewModel> FilteredNotes => _filteredNotes;
    public bool HasFilteredNotes  => _filteredNotes.Count > 0;
    public bool ShowNoteEmpty     => !HasNotes && !IsNoteFormOpen;
    public bool ShowNoteNoResults => HasNotes && IsNoteSearchActive && !HasFilteredNotes;

    // ── Reminders ─────────────────────────────────────────────────

    public ObservableCollection<ReminderItemViewModel> Reminders { get; } = new();

    // ── Reminder inline form ──────────────────────────────────────────────────

    private bool _isReminderFormOpen;
    public bool IsReminderFormOpen
    {
        get => _isReminderFormOpen;
        private set
        {
            if (SetProperty(ref _isReminderFormOpen, value))
            {
                OnPropertyChanged(nameof(ShowReminderEmpty));
                OnPropertyChanged(nameof(ShowReminderNoResults));
                OnPropertyChanged(nameof(ReminderFormTitleLabel));
            }
        }
    }

    private string? _editingReminderId;

    private string _reminderFormDate = string.Empty;
    public string ReminderFormDate
    {
        get => _reminderFormDate;
        set { if (SetProperty(ref _reminderFormDate, value)) { ReminderDateError = string.Empty; HasReminderDateError = false; } }
    }

    private string _reminderFormTitle = string.Empty;
    public string ReminderFormTitle
    {
        get => _reminderFormTitle;
        set { if (SetProperty(ref _reminderFormTitle, value)) ReminderTitleError = string.Empty; }
    }

    private string _reminderFormTime = "9:00";
    public string ReminderFormTime
    {
        get => _reminderFormTime;
        set { if (SetProperty(ref _reminderFormTime, value)) { ReminderTimeError = string.Empty; HasReminderTimeError = false; } }
    }

    private bool _reminderFormIsAm = true;
    public bool ReminderFormIsAm
    {
        get => _reminderFormIsAm;
        set
        {
            if (SetProperty(ref _reminderFormIsAm, value))
            {
                OnPropertyChanged(nameof(ReminderFormIsPm));
                OnPropertyChanged(nameof(ReminderAmPmLabel));
            }
        }
    }
    public bool   ReminderFormIsPm  => !_reminderFormIsAm;
    public string ReminderAmPmLabel => _reminderFormIsAm ? "AM" : "PM";

    private string _reminderFormNotes = string.Empty;
    public string ReminderFormNotes
    {
        get => _reminderFormNotes;
        set
        {
            if (SetProperty(ref _reminderFormNotes, value?.Length > 500 ? value[..500] : value ?? string.Empty))
                OnPropertyChanged(nameof(ReminderFormNotesLength));
        }
    }
    public int ReminderFormNotesLength => _reminderFormNotes.Length;

    private int _reminderFormRecurrenceIndex;
    public int ReminderFormRecurrenceIndex
    {
        get => _reminderFormRecurrenceIndex;
        set { if (SetProperty(ref _reminderFormRecurrenceIndex, value)) OnPropertyChanged(nameof(ShowReminderEndDate)); }
    }
    public bool ShowReminderEndDate => _reminderFormRecurrenceIndex > 0;

    private string _reminderFormEndDate = string.Empty;
    public string ReminderFormEndDate
    {
        get => _reminderFormEndDate;
        set { if (SetProperty(ref _reminderFormEndDate, value)) { ReminderEndDateError = string.Empty; HasReminderEndDateError = false; } }
    }

    // Validation
    private string _reminderTitleError = string.Empty;
    public string ReminderTitleError
    {
        get => _reminderTitleError;
        private set { if (SetProperty(ref _reminderTitleError, value)) OnPropertyChanged(nameof(HasReminderTitleError)); }
    }
    public bool HasReminderTitleError => !string.IsNullOrEmpty(_reminderTitleError);

    private string _reminderDateError = string.Empty;
    public string ReminderDateError
    {
        get => _reminderDateError;
        private set => SetProperty(ref _reminderDateError, value);
    }
    private bool _hasReminderDateError;
    public bool HasReminderDateError
    {
        get => _hasReminderDateError;
        private set => SetProperty(ref _hasReminderDateError, value);
    }

    private string _reminderTimeError = string.Empty;
    public string ReminderTimeError
    {
        get => _reminderTimeError;
        private set => SetProperty(ref _reminderTimeError, value);
    }
    private bool _hasReminderTimeError;
    public bool HasReminderTimeError
    {
        get => _hasReminderTimeError;
        private set => SetProperty(ref _hasReminderTimeError, value);
    }

    private string _reminderEndDateError = string.Empty;
    public string ReminderEndDateError
    {
        get => _reminderEndDateError;
        private set => SetProperty(ref _reminderEndDateError, value);
    }
    private bool _hasReminderEndDateError;
    public bool HasReminderEndDateError
    {
        get => _hasReminderEndDateError;
        private set => SetProperty(ref _hasReminderEndDateError, value);
    }

    public string ReminderFormTitleLabel => _editingReminderId is null
        ? _loc.Get("reminders.form_title_add")
        : _loc.Get("reminders.form_title_edit");

    // ── Reminder search ───────────────────────────────────────────────────────

    private string _reminderSearchText = string.Empty;
    public string ReminderSearchText
    {
        get => _reminderSearchText;
        set
        {
            if (SetProperty(ref _reminderSearchText, value))
            {
                OnPropertyChanged(nameof(IsReminderSearchActive));
                OnPropertyChanged(nameof(ReminderSearchClearVisible));
                UpdateFilteredReminders();
            }
        }
    }
    public bool IsReminderSearchActive    => !string.IsNullOrWhiteSpace(_reminderSearchText);
    public bool ReminderSearchClearVisible => IsReminderSearchActive;

    private bool _showCompletedReminders;
    public bool ShowCompletedReminders
    {
        get => _showCompletedReminders;
        private set
        {
            if (SetProperty(ref _showCompletedReminders, value))
            {
                OnPropertyChanged(nameof(ToggleCompletedLabel));
                UpdateFilteredReminders();
            }
        }
    }
    public int  CompletedRemindersCount => Reminders.Count(r => r.IsCompleted);
    public bool HasCompletedReminders   => CompletedRemindersCount > 0;
    public string ToggleCompletedLabel  => _showCompletedReminders ? HideCompletedLabel : ShowCompletedLabel;

    private IReadOnlyList<ReminderItemViewModel> _filteredReminders = Array.Empty<ReminderItemViewModel>();
    public  IReadOnlyList<ReminderItemViewModel> FilteredReminders => _filteredReminders;
    public bool HasFilteredReminders  => _filteredReminders.Count > 0;
    public bool ShowReminderEmpty     => !HasReminders && !IsReminderFormOpen;
    public bool ShowReminderNoResults => HasReminders && IsReminderSearchActive && !HasFilteredReminders;

    // ── Documents ────────────────────────────────────────────────────────────

    public ObservableCollection<DocItemViewModel> Documents { get; } = new();

    // Title quick-fill presets
    public static IReadOnlyList<string> DocTitlePresets { get; } = new[]
    {
        "Citizenship Front", "Citizenship Back", "Passport", "PAN Card", "Voter ID",
    };

    // Tag presets
    public static IReadOnlyList<string> DocTagPresets { get; } = new[]
    {
        "Personal", "Government", "Education", "Financial",
        "Medical", "Work", "Property", "Insurance", "Legal",
    };

    private string? _editingDocId;

    private bool _isDocFormOpen;
    public bool IsDocFormOpen
    {
        get => _isDocFormOpen;
        private set
        {
            if (SetProperty(ref _isDocFormOpen, value))
            {
                OnPropertyChanged(nameof(ShowDocEmpty));
                OnPropertyChanged(nameof(ShowDocNoResults));
            }
        }
    }

    private bool _isEditingDoc;
    public bool IsEditingDoc
    {
        get => _isEditingDoc;
        private set
        {
            if (SetProperty(ref _isEditingDoc, value))
                OnPropertyChanged(nameof(DocFormTitleLabel));
        }
    }

    public string DocFormTitleLabel => _isEditingDoc
        ? _loc.Get("docs.form_title_edit")
        : _loc.Get("docs.form_title_add");

    // Form fields
    private static readonly HashSet<char> _invalidTitleChars = new(Path.GetInvalidFileNameChars());

    private string _docEditTitle = string.Empty;
    public string DocEditTitle
    {
        get => _docEditTitle;
        set
        {
            var clean = new string(value.Where(c => !_invalidTitleChars.Contains(c)).ToArray());
            SetProperty(ref _docEditTitle, clean);
        }
    }

    public ObservableCollection<string> DocEditTags { get; } = new();

    private string _docEditTagInput = string.Empty;
    public string DocEditTagInput
    {
        get => _docEditTagInput;
        set => SetProperty(ref _docEditTagInput, value);
    }

    private string _docEditFilePath = string.Empty;
    public string DocEditFilePath
    {
        get => _docEditFilePath;
        set
        {
            if (SetProperty(ref _docEditFilePath, value))
            {
                OnPropertyChanged(nameof(DocEditHasFile));
                OnPropertyChanged(nameof(DocEditFileName));
                OnPropertyChanged(nameof(DocEditFileExtension));
            }
        }
    }
    public bool   DocEditHasFile        => !string.IsNullOrWhiteSpace(_docEditFilePath);
    public string DocEditFileName       => DocEditHasFile ? Path.GetFileName(_docEditFilePath) : string.Empty;
    public string DocEditFileExtension  => DocEditHasFile ? Path.GetExtension(_docEditFilePath).TrimStart('.').ToUpperInvariant() : string.Empty;

    private string _docEditNotes = string.Empty;
    public string DocEditNotes
    {
        get => _docEditNotes;
        set => SetProperty(ref _docEditNotes, value);
    }

    private string _docEditError = string.Empty;
    public string DocEditError
    {
        get => _docEditError;
        private set
        {
            if (SetProperty(ref _docEditError, value))
                OnPropertyChanged(nameof(HasDocEditError));
        }
    }
    public bool HasDocEditError => !string.IsNullOrEmpty(_docEditError);

    // Search
    private string _docSearchText = string.Empty;
    public string DocSearchText
    {
        get => _docSearchText;
        set
        {
            if (SetProperty(ref _docSearchText, value))
            {
                OnPropertyChanged(nameof(IsDocSearchActive));
                OnPropertyChanged(nameof(DocSearchClearVisible));
                UpdateFilteredDocuments();
                UpdateSearchSuggestions();
            }
        }
    }
    public bool IsDocSearchActive    => !string.IsNullOrWhiteSpace(_docSearchText);
    public bool DocSearchClearVisible => IsDocSearchActive;

    private IReadOnlyList<DocItemViewModel> _filteredDocuments = Array.Empty<DocItemViewModel>();
    public  IReadOnlyList<DocItemViewModel> FilteredDocuments => _filteredDocuments;
    public bool HasFilteredDocuments => _filteredDocuments.Count > 0;

    public bool ShowDocEmpty     => !HasDocuments && !IsDocFormOpen;
    public bool ShowDocNoResults => HasDocuments && IsDocSearchActive && !HasFilteredDocuments;

    public ObservableCollection<string> DocSearchSuggestions { get; } = new();

    private bool _isDocSuggestionsOpen;
    public bool IsDocSuggestionsOpen
    {
        get => _isDocSuggestionsOpen;
        set => SetProperty(ref _isDocSuggestionsOpen, value);
    }

    private bool _isSearchBoxFocused;

    // ── Labels ───────────────────────────────────────────────────────────────

    public string NotesHeadingLabel    { get; private set; } = string.Empty;
    public string RemindersHeadingLabel{ get; private set; } = string.Empty;
    public string NoNotesLabel         { get; private set; } = string.Empty;
    public string NoRemindersLabel     { get; private set; } = string.Empty;
    public string NoNotesHintLabel     { get; private set; } = string.Empty;
    public string NoRemindersHintLabel { get; private set; } = string.Empty;
    public string AddNoteLabel              { get; private set; } = string.Empty;
    public string NoteSearchHintLabel       { get; private set; } = string.Empty;
    public string NoNoteResultsLabel        { get; private set; } = string.Empty;
    public string AddReminderLabel          { get; private set; } = string.Empty;
    public string ReminderSearchHintLabel   { get; private set; } = string.Empty;
    public string NoReminderResultsLabel    { get; private set; } = string.Empty;
    public string ShowCompletedLabel        { get; private set; } = string.Empty;
    public string HideCompletedLabel        { get; private set; } = string.Empty;
    public string ReminderFieldTitleLabel   { get; private set; } = string.Empty;
    public string ReminderFieldDateLabel    { get; private set; } = string.Empty;
    public string ReminderFieldTimeLabel    { get; private set; } = string.Empty;
    public string ReminderFieldNotesLabel   { get; private set; } = string.Empty;
    public string ReminderFieldRecurLabel   { get; private set; } = string.Empty;
    public string ReminderFieldEndDateLabel { get; private set; } = string.Empty;
    public string ReminderDateInvalidLabel  { get; private set; } = string.Empty;
    public string ReminderDatePastLabel     { get; private set; } = string.Empty;
    public string ReminderTimeInvalidLabel  { get; private set; } = string.Empty;
    public string ReminderEndDateInvalidLabel    { get; private set; } = string.Empty;
    public string ReminderEndDateBeforeStartLabel{ get; private set; } = string.Empty;
    public string ReminderTitleRequiredLabel     { get; private set; } = string.Empty;
    public string NoteFieldDateLabel        { get; private set; } = string.Empty;
    public string NoteFieldTextLabel        { get; private set; } = string.Empty;
    public string NoteDateFormatHintLabel   { get; private set; } = string.Empty;
    public IReadOnlyList<string> ReminderRecurrenceOptions { get; private set; } = Array.Empty<string>();
    public string HintReminderTitle         { get; private set; } = string.Empty;
    public string HintReminderDate          { get; private set; } = string.Empty;
    public string HintReminderTime          { get; private set; } = string.Empty;
    public string HintReminderNotes         { get; private set; } = string.Empty;
    public string SaveLabel            { get; private set; } = string.Empty;
    public string CancelLabel          { get; private set; } = string.Empty;
    public string DeleteLabel          { get; private set; } = string.Empty;
    public string EditLabel            { get; private set; } = string.Empty;

    public string DocsHeadingLabel      { get; private set; } = string.Empty;
    public string NoDocsLabel           { get; private set; } = string.Empty;
    public string NoDocsHintLabel       { get; private set; } = string.Empty;
    public string NoDocsResultsLabel    { get; private set; } = string.Empty;
    public string AddDocLabel           { get; private set; } = string.Empty;
    public string DocBrowseLabel        { get; private set; } = string.Empty;
    public string DocOpenLabel          { get; private set; } = string.Empty;
    public string DocFileNotFoundLabel  { get; private set; } = string.Empty;
    public string DocTitleRequiredLabel { get; private set; } = string.Empty;
    public string DocFileRequiredLabel  { get; private set; } = string.Empty;
    public string DocNoFileHintLabel    { get; private set; } = string.Empty;
    public string DocFieldTitleLabel    { get; private set; } = string.Empty;
    public string DocFieldTagsLabel     { get; private set; } = string.Empty;
    public string DocFieldFileLabel     { get; private set; } = string.Empty;
    public string DocFieldNotesLabel    { get; private set; } = string.Empty;
    public string DocClearFileLabel     { get; private set; } = string.Empty;
    public string DocSearchHintLabel    { get; private set; } = string.Empty;
    public string DocDuplicateTitleLabel { get; private set; } = string.Empty;

    // ── Commands ─────────────────────────────────────────────────────────────

    public ICommand DeleteNoteCommand        { get; }
    public ICommand StartEditNoteCommand     { get; }
    public ICommand SaveNoteCommand          { get; }
    public ICommand CancelNoteEditCommand    { get; }
    public ICommand AddNoteCommand           { get; }
    public ICommand ClearNoteSearchCommand   { get; }
    public ICommand SaveNoteFormCommand      { get; }
    public ICommand CancelNoteFormCommand    { get; }
    public ICommand DeleteReminderCommand    { get; }
    public ICommand EditReminderCommand      { get; }
    public ICommand AddNewReminderCommand    { get; }
    public ICommand ClearReminderSearchCommand  { get; }
    public ICommand ToggleShowCompletedCommand   { get; }
    public ICommand SaveReminderFormCommand  { get; }
    public ICommand CancelReminderFormCommand{ get; }
    public ICommand SetReminderAmCommand { get; }
    public ICommand SetReminderPmCommand { get; }
    public ICommand ToggleReminderAmPmCommand { get; }

    public ICommand ShowAddDocumentCommand      { get; }
    public ICommand SaveDocumentCommand         { get; }
    public ICommand CancelDocumentEditCommand   { get; }
    public ICommand BrowseDocumentFileCommand   { get; }
    public ICommand ClearDocumentFileCommand    { get; }
    public ICommand DeleteDocumentCommand       { get; }
    public ICommand EditDocumentCommand         { get; }
    public ICommand OpenDocumentCommand         { get; }
    public ICommand OpenDocumentFolderCommand   { get; }
    public ICommand SetDocTitlePresetCommand    { get; }
    public ICommand AddDocTagPresetCommand      { get; }
    public ICommand AddDocTagFromInputCommand   { get; }
    public ICommand RemoveDocTagCommand         { get; }
    public ICommand DocSearchGotFocusCommand    { get; }
    public ICommand DocSearchLostFocusCommand   { get; }
    public ICommand SelectDocSuggestionCommand  { get; }
    public ICommand CommitDocSearchCommand      { get; }
    public ICommand ClearDocSearchCommand       { get; }

    // No popup events — add/edit is now handled inline within this ViewModel.

    // ── Construction ─────────────────────────────────────────────────────────

    // We need the adapter for date validation in reminder form
    private INepaliDateAdapter? _adapter;

    public MoreViewModel(
        ILocalizationService localizationService,
        INotesService? notesService = null,
        IReminderService? reminderService = null,
        IDocumentService? documentService = null,
        INepaliDateAdapter? adapter = null)
    {
        _loc            = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        _notesService   = notesService;
        _reminderService = reminderService;
        _documentService = documentService;
        _adapter        = adapter;

        DeleteNoteCommand        = new RelayCommand<string>(DoDeleteNote);
        StartEditNoteCommand     = new RelayCommand<string>(DoStartEditNote);
        SaveNoteCommand          = new RelayCommand(DoSaveNote);
        CancelNoteEditCommand    = new RelayCommand(DoCancelNoteEdit);
        AddNoteCommand           = new RelayCommand(DoShowAddNoteForm);
        ClearNoteSearchCommand   = new RelayCommand(() => NoteSearchText = string.Empty);
        SaveNoteFormCommand      = new RelayCommand(DoSaveNoteForm);
        CancelNoteFormCommand    = new RelayCommand(DoCancelNoteForm);
        DeleteReminderCommand    = new RelayCommand<string>(DoDeleteReminder);
        EditReminderCommand      = new RelayCommand<string>(DoShowEditReminderForm);
        AddNewReminderCommand    = new RelayCommand(DoShowAddReminderForm);
        ClearReminderSearchCommand       = new RelayCommand(() => ReminderSearchText = string.Empty);
        ToggleShowCompletedCommand        = new RelayCommand(() => ShowCompletedReminders = !_showCompletedReminders);
        SaveReminderFormCommand  = new RelayCommand(DoSaveReminderForm);
        CancelReminderFormCommand= new RelayCommand(DoCancelReminderForm);
        SetReminderAmCommand      = new RelayCommand(() => ReminderFormIsAm = true);
        SetReminderPmCommand      = new RelayCommand(() => ReminderFormIsAm = false);
        ToggleReminderAmPmCommand = new RelayCommand(() => ReminderFormIsAm = !_reminderFormIsAm);

        SetModeDocumentsCommand = new RelayCommand(() => SetMode(0));
        SetModeNotesCommand     = new RelayCommand(() => SetMode(1));
        SetModeRemindersCommand = new RelayCommand(() => SetMode(2));

        ShowAddDocumentCommand    = new RelayCommand(DoShowAddDocument);
        SaveDocumentCommand       = new RelayCommand(DoSaveDocument);
        CancelDocumentEditCommand = new RelayCommand(DoCancelDocumentEdit);
        BrowseDocumentFileCommand = new RelayCommand(DoBrowseDocumentFile);
        ClearDocumentFileCommand  = new RelayCommand(() => DocEditFilePath = string.Empty);
        DeleteDocumentCommand     = new RelayCommand<string>(DoDeleteDocument);
        EditDocumentCommand       = new RelayCommand<string>(DoEditDocument);
        OpenDocumentCommand       = new RelayCommand<string>(DoOpenDocument);
        OpenDocumentFolderCommand = new RelayCommand<string>(DoOpenDocumentFolder);
        SetDocTitlePresetCommand  = new RelayCommand<string>(t => { if (t is not null) DocEditTitle = t; });
        AddDocTagPresetCommand    = new RelayCommand<string>(DoAddDocTagPreset);
        AddDocTagFromInputCommand = new RelayCommand(DoAddDocTagFromInput);
        RemoveDocTagCommand       = new RelayCommand<string>(DoRemoveDocTag);
        DocSearchGotFocusCommand  = new RelayCommand(DoDocSearchGotFocus);
        DocSearchLostFocusCommand = new RelayCommand(DoDocSearchLostFocus);
        SelectDocSuggestionCommand = new RelayCommand<string>(DoSelectDocSuggestion);
        CommitDocSearchCommand    = new RelayCommand(CommitDocSearch);
        ClearDocSearchCommand     = new RelayCommand(DoClearDocSearch);

        if (_notesService is not null)
            _notesService.NotesChanged += (_, _) => RefreshNotes();
        if (_reminderService is not null)
            _reminderService.RemindersChanged += (_, _) => RefreshReminders();
        if (_documentService is not null)
            _documentService.DocumentsChanged += (_, _) => RefreshDocuments();

        RefreshLabels();
        RefreshNotes();
        RefreshReminders();
        RefreshDocuments();
    }

    // ── Public navigation entry points (called from MainWindow after calendar popup) ──

    /// <summary>Switches to Notes tab and opens the add form pre-filled with the given BS date key (YYYY-MM-DD).</summary>
    public void OpenNoteForm(string dateKey)
    {
        SetMode(1);
        DoCancelNoteForm();
        var existing = _notesService?.GetNote(dateKey);
        // Use edit mode if note already exists so save overwrites rather than appends
        _editingNoteKey = existing is not null ? dateKey : null;
        _noteFormDateInput = dateKey.Replace('-', '/');
        OnPropertyChanged(nameof(NoteFormDateInput));
        _noteFormText = existing ?? string.Empty;
        OnPropertyChanged(nameof(NoteFormText));
        OnPropertyChanged(nameof(NoteFormTitleLabel));
        IsNoteFormOpen = true;
    }

    /// <summary>Switches to Reminders tab and opens the add form pre-filled with the given BS date.</summary>
    public void OpenReminderForm(int bsYear, int bsMonth, int bsDay)
    {
        SetMode(2);
        DoCancelReminderForm();
        _editingReminderId = null;
        _reminderFormDate = ReminderEntry.FormatDate(bsYear, bsMonth, bsDay);
        OnPropertyChanged(nameof(ReminderFormDate));
        _reminderFormTitle = string.Empty;
        OnPropertyChanged(nameof(ReminderFormTitle));
        // Pre-fill current system time
        SetReminderFormTimeFrom24(DateTime.Now.ToString("HH:mm"));
        _reminderFormNotes = string.Empty;
        OnPropertyChanged(nameof(ReminderFormNotes));
        _reminderFormRecurrenceIndex = 0;
        OnPropertyChanged(nameof(ReminderFormRecurrenceIndex));
        _reminderFormEndDate = string.Empty;
        OnPropertyChanged(nameof(ReminderFormEndDate));
        IsReminderFormOpen = true;
    }

    public void OnLanguageChanged()
    {
        RefreshLabels();
        RefreshNotes();
        RefreshReminders();
        RefreshDocuments();
    }

    /// <summary>
    /// Opens the reminder form pre-filled with an existing reminder for editing.
    /// Called when the user clicks Edit on a reminder from the day-info popup.
    /// </summary>
    public void OpenEditReminderForm(string reminderId)
    {
        SetMode(2);
        DoShowEditReminderForm(reminderId);
    }

    /// <summary>
    /// Switches to Reminders mode and temporarily highlights the given reminder.
    /// Called when the user clicks a notification popup.
    /// </summary>
    public void NavigateToReminder(string reminderId)
    {
        RefreshReminders();
        SetMode(2);

        foreach (var r in Reminders)
            r.IsHighlighted = r.Id == reminderId;

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
        NotesHeadingLabel    = _loc.Get("more.notes_heading");
        RemindersHeadingLabel= _loc.Get("more.reminders_heading");
        NoNotesLabel         = _loc.Get("more.no_notes");
        NoRemindersLabel     = _loc.Get("more.no_reminders");
        NoNotesHintLabel     = _loc.Get("more.no_notes_hint");
        NoRemindersHintLabel = _loc.Get("more.no_reminders_hint");
        AddNoteLabel              = _loc.Get("notes.add");
        NoteSearchHintLabel       = _loc.Get("notes.search_hint");
        NoNoteResultsLabel        = _loc.Get("notes.no_results");
        NoteFieldDateLabel        = _loc.Get("notes.field_date");
        NoteFieldTextLabel        = _loc.Get("notes.field_text");
        NoteDateFormatHintLabel   = _loc.Get("hint.date_bs");
        AddReminderLabel          = _loc.Get("reminders.add");
        ReminderSearchHintLabel   = _loc.Get("reminders.search_hint");
        NoReminderResultsLabel    = _loc.Get("reminders.no_results");
        ShowCompletedLabel        = _loc.Get("reminders.show_completed");
        HideCompletedLabel        = _loc.Get("reminders.hide_completed");
        OnPropertyChanged(nameof(ToggleCompletedLabel));
        ReminderFieldTitleLabel   = _loc.Get("reminder.title");
        ReminderFieldDateLabel    = _loc.Get("reminder.date");
        ReminderFieldTimeLabel    = _loc.Get("reminder.time");
        ReminderFieldNotesLabel   = _loc.Get("reminder.notes");
        ReminderFieldRecurLabel   = _loc.Get("reminder.recurrence");
        ReminderFieldEndDateLabel = _loc.Get("reminder.end_date");
        ReminderDateInvalidLabel  = _loc.Get("reminder.date_invalid");
        ReminderDatePastLabel     = _loc.Get("reminder.date_past");
        ReminderTimeInvalidLabel  = _loc.Get("reminder.time_invalid");
        ReminderEndDateInvalidLabel     = _loc.Get("reminder.end_date_invalid");
        ReminderEndDateBeforeStartLabel = _loc.Get("reminder.end_date_before_start");
        ReminderTitleRequiredLabel      = _loc.Get("reminder.title_required");
        HintReminderTitle         = _loc.Get("hint.reminder_title");
        HintReminderDate          = _loc.Get("hint.date_bs");
        HintReminderTime          = _loc.Get("hint.reminder_time");
        HintReminderNotes         = _loc.Get("hint.reminder_notes");
        ReminderRecurrenceOptions = new[]
        {
            _loc.Get("reminder.recurrence_none"),
            _loc.Get("reminder.recurrence_daily"),
            _loc.Get("reminder.recurrence_weekly"),
            _loc.Get("reminder.recurrence_monthly"),
            _loc.Get("reminder.recurrence_yearly"),
        };
        SaveLabel            = _loc.Get("more.save");
        CancelLabel          = _loc.Get("more.cancel");
        DeleteLabel          = _loc.Get("more.delete");
        EditLabel            = _loc.Get("more.edit");

        DocsHeadingLabel      = _loc.Get("docs.heading");
        NoDocsLabel           = _loc.Get("docs.no_docs");
        NoDocsHintLabel       = _loc.Get("docs.no_docs_hint");
        NoDocsResultsLabel    = _loc.Get("docs.no_results");
        AddDocLabel           = _loc.Get("docs.add");
        DocBrowseLabel        = _loc.Get("docs.browse");
        DocOpenLabel          = _loc.Get("docs.open");
        DocFileNotFoundLabel  = _loc.Get("docs.file_not_found");
        DocTitleRequiredLabel = _loc.Get("docs.title_required");
        DocFileRequiredLabel  = _loc.Get("docs.file_required");
        DocNoFileHintLabel    = _loc.Get("docs.no_file_hint");
        DocFieldTitleLabel    = _loc.Get("docs.field_title");
        DocFieldTagsLabel     = _loc.Get("docs.field_tags");
        DocFieldFileLabel     = _loc.Get("docs.field_file");
        DocFieldNotesLabel    = _loc.Get("docs.field_notes");
        DocClearFileLabel     = _loc.Get("docs.clear_file");
        DocSearchHintLabel    = _loc.Get("docs.search_hint");
        DocDuplicateTitleLabel = _loc.Get("docs.duplicate_title");

        OnPropertyChanged(nameof(NotesHeadingLabel));
        OnPropertyChanged(nameof(RemindersHeadingLabel));
        OnPropertyChanged(nameof(NoNotesLabel));
        OnPropertyChanged(nameof(NoRemindersLabel));
        OnPropertyChanged(nameof(NoNotesHintLabel));
        OnPropertyChanged(nameof(NoRemindersHintLabel));
        OnPropertyChanged(nameof(AddNoteLabel));
        OnPropertyChanged(nameof(NoteSearchHintLabel));
        OnPropertyChanged(nameof(NoNoteResultsLabel));
        OnPropertyChanged(nameof(NoteFieldDateLabel));
        OnPropertyChanged(nameof(NoteFieldTextLabel));
        OnPropertyChanged(nameof(NoteDateFormatHintLabel));
        OnPropertyChanged(nameof(AddReminderLabel));
        OnPropertyChanged(nameof(ReminderSearchHintLabel));
        OnPropertyChanged(nameof(NoReminderResultsLabel));
        OnPropertyChanged(nameof(ReminderFieldTitleLabel));
        OnPropertyChanged(nameof(ReminderFieldDateLabel));
        OnPropertyChanged(nameof(ReminderFieldTimeLabel));
        OnPropertyChanged(nameof(ReminderFieldNotesLabel));
        OnPropertyChanged(nameof(ReminderFieldRecurLabel));
        OnPropertyChanged(nameof(ReminderFieldEndDateLabel));
        OnPropertyChanged(nameof(ReminderDateInvalidLabel));
        OnPropertyChanged(nameof(ReminderDatePastLabel));
        OnPropertyChanged(nameof(ReminderTimeInvalidLabel));
        OnPropertyChanged(nameof(ReminderEndDateInvalidLabel));
        OnPropertyChanged(nameof(ReminderEndDateBeforeStartLabel));
        OnPropertyChanged(nameof(ReminderTitleRequiredLabel));
        OnPropertyChanged(nameof(HintReminderTitle));
        OnPropertyChanged(nameof(HintReminderDate));
        OnPropertyChanged(nameof(HintReminderTime));
        OnPropertyChanged(nameof(HintReminderNotes));
        OnPropertyChanged(nameof(ReminderRecurrenceOptions));
        OnPropertyChanged(nameof(SaveLabel));
        OnPropertyChanged(nameof(CancelLabel));
        OnPropertyChanged(nameof(DeleteLabel));
        OnPropertyChanged(nameof(EditLabel));
        OnPropertyChanged(nameof(DocsHeadingLabel));
        OnPropertyChanged(nameof(NoDocsLabel));
        OnPropertyChanged(nameof(NoDocsHintLabel));
        OnPropertyChanged(nameof(NoDocsResultsLabel));
        OnPropertyChanged(nameof(AddDocLabel));
        OnPropertyChanged(nameof(DocBrowseLabel));
        OnPropertyChanged(nameof(DocOpenLabel));
        OnPropertyChanged(nameof(DocFileNotFoundLabel));
        OnPropertyChanged(nameof(DocTitleRequiredLabel));
        OnPropertyChanged(nameof(DocFileRequiredLabel));
        OnPropertyChanged(nameof(DocNoFileHintLabel));
        OnPropertyChanged(nameof(DocFormTitleLabel));
        OnPropertyChanged(nameof(DocFieldTitleLabel));
        OnPropertyChanged(nameof(DocFieldTagsLabel));
        OnPropertyChanged(nameof(DocFieldFileLabel));
        OnPropertyChanged(nameof(DocFieldNotesLabel));
        OnPropertyChanged(nameof(DocClearFileLabel));
        OnPropertyChanged(nameof(DocSearchHintLabel));
        OnPropertyChanged(nameof(DocDuplicateTitleLabel));
    }

    public void RefreshNotes()
    {
        Notes.Clear();
        _editingNoteKey = null;
        if (_notesService is null) return;
        foreach (var (key, value) in _notesService.GetAll().OrderByDescending(kv => kv.Key))
            Notes.Add(new NoteItemViewModel(key, value));
        OnPropertyChanged(nameof(HasNotes));
        OnPropertyChanged(nameof(ShowNoteEmpty));
        UpdateFilteredNotes();
    }

    private void UpdateFilteredNotes()
    {
        var q = _noteSearchText.Trim();
        _filteredNotes = string.IsNullOrEmpty(q)
            ? Notes.ToList()
            : Notes.Where(n =>
                n.Text.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                n.DisplayDate.Contains(q, StringComparison.OrdinalIgnoreCase))
              .ToList();
        OnPropertyChanged(nameof(FilteredNotes));
        OnPropertyChanged(nameof(HasFilteredNotes));
        OnPropertyChanged(nameof(ShowNoteNoResults));
    }

    // ── Note form operations ──────────────────────────────────────────────────

    private void DoShowAddNoteForm()
    {
        DoCancelNoteForm();
        _editingNoteKey = null;
        // Pre-fill today's BS date when adapter is available
        if (_adapter is not null)
        {
            var (y, m, d) = _adapter.GetTodayBs();
            _noteFormDateInput = $"{y:D4}/{m:D2}/{d:D2}";
        }
        else
        {
            _noteFormDateInput = string.Empty;
        }
        OnPropertyChanged(nameof(NoteFormDateInput));
        _noteFormText = string.Empty;
        OnPropertyChanged(nameof(NoteFormText));
        NoteFormError = string.Empty;
        IsNoteFormOpen = true;
    }

    private void DoCancelNoteForm()
    {
        _editingNoteKey = null;
        _noteFormDateInput = string.Empty;
        OnPropertyChanged(nameof(NoteFormDateInput));
        _noteFormText = string.Empty;
        OnPropertyChanged(nameof(NoteFormText));
        NoteFormError = string.Empty;
        IsNoteFormOpen = false;
    }

    private void DoSaveNoteForm()
    {
        var dateKey = NoteFormDateInput.Trim();
        if (string.IsNullOrEmpty(dateKey))
        {
            NoteFormError = _loc.Get("notes.date_required");
            return;
        }
        // Validate YYYY-MM-DD or YYYY/MM/DD format
        if (!System.Text.RegularExpressions.Regex.IsMatch(dateKey, @"^\d{4}[/-]\d{2}[/-]\d{2}$"))
        {
            NoteFormError = _loc.Get("notes.date_invalid");
            return;
        }
        // Normalize to YYYY-MM-DD
        dateKey = dateKey.Replace('/', '-');
        var text = NoteFormText.Trim();
        if (string.IsNullOrEmpty(text))
        {
            NoteFormError = _loc.Get("notes.text_required");
            return;
        }
        if (text.Length > 500)
        {
            NoteFormError = _loc.Get("notes.text_too_long");
            return;
        }

        // Append if note exists for this date and we're adding (not editing)
        if (_editingNoteKey is null)
        {
            var existing = _notesService?.GetNote(dateKey);
            if (!string.IsNullOrEmpty(existing))
                text = existing + "\n" + text;
        }

        _notesService?.SetNote(dateKey, text);
        NoteFormError = string.Empty;
        IsNoteFormOpen = false;
        _editingNoteKey = null;
        RefreshNotes();
    }

    public void RefreshReminders()
    {
        Reminders.Clear();
        if (_reminderService is null) return;
        foreach (var r in _reminderService.GetAll().OrderByDescending(r => r.BsDate))
            Reminders.Add(new ReminderItemViewModel(r));
        OnPropertyChanged(nameof(HasReminders));
        OnPropertyChanged(nameof(ShowReminderEmpty));
        OnPropertyChanged(nameof(CompletedRemindersCount));
        OnPropertyChanged(nameof(HasCompletedReminders));
        UpdateFilteredReminders();
    }

    private void UpdateFilteredReminders()
    {
        var q = _reminderSearchText.Trim();
        var source = _showCompletedReminders
            ? Reminders
            : (IEnumerable<ReminderItemViewModel>)Reminders.Where(r => !r.IsCompleted);
        _filteredReminders = string.IsNullOrEmpty(q)
            ? source.ToList()
            : source.Where(r =>
                r.Title.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Notes.Contains(q, StringComparison.OrdinalIgnoreCase) ||
                r.Date.Contains(q, StringComparison.OrdinalIgnoreCase))
              .ToList();
        OnPropertyChanged(nameof(FilteredReminders));
        OnPropertyChanged(nameof(HasFilteredReminders));
        OnPropertyChanged(nameof(ShowReminderNoResults));
        OnPropertyChanged(nameof(CompletedRemindersCount));
        OnPropertyChanged(nameof(HasCompletedReminders));
    }

    // ── Reminder form operations ──────────────────────────────────────────────

    private static readonly Regex _timePattern = new(@"^(\d{1,2}):(\d{2})$", RegexOptions.Compiled);

    private void DoShowAddReminderForm()
    {
        DoCancelReminderForm();
        _editingReminderId = null;
        // Pre-fill today's BS date
        if (_adapter is not null)
        {
            var (y, m, d) = _adapter.GetTodayBs();
            _reminderFormDate = ReminderEntry.FormatDate(y, m, d);
        }
        else
        {
            _reminderFormDate = string.Empty;
        }
        OnPropertyChanged(nameof(ReminderFormDate));
        _reminderFormTitle = string.Empty;
        OnPropertyChanged(nameof(ReminderFormTitle));
        // Pre-fill current system time
        SetReminderFormTimeFrom24(DateTime.Now.ToString("HH:mm"));
        _reminderFormNotes = string.Empty;
        OnPropertyChanged(nameof(ReminderFormNotes));
        _reminderFormRecurrenceIndex = 0;
        OnPropertyChanged(nameof(ReminderFormRecurrenceIndex));
        _reminderFormEndDate = string.Empty;
        OnPropertyChanged(nameof(ReminderFormEndDate));
        IsReminderFormOpen = true;
    }

    private void DoCancelReminderForm()
    {
        _editingReminderId = null;
        IsReminderFormOpen = false;
        ReminderTitleError = string.Empty;
        ReminderDateError = string.Empty;
        ReminderTimeError = string.Empty;
        ReminderEndDateError = string.Empty;
        HasReminderDateError = false;
        HasReminderTimeError = false;
        HasReminderEndDateError = false;
    }

    private void DoShowEditReminderForm(string? id)
    {
        if (id is null || _reminderService is null) return;
        var entry = _reminderService.GetAll().FirstOrDefault(r => r.Id == id);
        if (entry is null) return;
        DoCancelReminderForm();
        _editingReminderId = id;
        _reminderFormDate = entry.BsDate;
        OnPropertyChanged(nameof(ReminderFormDate));
        _reminderFormTitle = entry.Title;
        OnPropertyChanged(nameof(ReminderFormTitle));
        SetReminderFormTimeFrom24(entry.Time);
        _reminderFormNotes = entry.Notes;
        OnPropertyChanged(nameof(ReminderFormNotes));
        _reminderFormRecurrenceIndex = (int)entry.Recurrence;
        OnPropertyChanged(nameof(ReminderFormRecurrenceIndex));
        _reminderFormEndDate = entry.EndDate ?? string.Empty;
        OnPropertyChanged(nameof(ReminderFormEndDate));
        OnPropertyChanged(nameof(ReminderFormTitleLabel));
        IsReminderFormOpen = true;
    }

    private void SetReminderFormTimeFrom24(string hhmm)
    {
        if (!TimeSpan.TryParse(hhmm, out var ts)) { _reminderFormTime = "9:00"; _reminderFormIsAm = true; OnPropertyChanged(nameof(ReminderFormTime)); OnPropertyChanged(nameof(ReminderFormIsAm)); OnPropertyChanged(nameof(ReminderFormIsPm)); OnPropertyChanged(nameof(ReminderAmPmLabel)); return; }
        int h = ts.Hours, m = ts.Minutes;
        if (h == 0)       { _reminderFormIsAm = true;  _reminderFormTime = $"12:{m:D2}"; }
        else if (h < 12)  { _reminderFormIsAm = true;  _reminderFormTime = $"{h}:{m:D2}"; }
        else if (h == 12) { _reminderFormIsAm = false; _reminderFormTime = $"12:{m:D2}"; }
        else              { _reminderFormIsAm = false; _reminderFormTime = $"{h - 12}:{m:D2}"; }
        OnPropertyChanged(nameof(ReminderFormTime));
        OnPropertyChanged(nameof(ReminderFormIsAm));
        OnPropertyChanged(nameof(ReminderFormIsPm));
        OnPropertyChanged(nameof(ReminderAmPmLabel));
    }

    private static bool TryParseTime12(string input, bool isAm, out string time24)
    {
        time24 = string.Empty;
        if (string.IsNullOrWhiteSpace(input)) return false;
        var match = _timePattern.Match(input.Trim());
        if (!match.Success) return false;
        int h = int.Parse(match.Groups[1].Value);
        int m = int.Parse(match.Groups[2].Value);
        if (h < 1 || h > 12 || m < 0 || m > 59) return false;
        int h24 = isAm ? (h == 12 ? 0 : h) : (h == 12 ? 12 : h + 12);
        time24 = $"{h24:D2}:{m:D2}";
        return true;
    }

    private void DoSaveReminderForm()
    {
        if (_reminderService is null) return;

        if (string.IsNullOrWhiteSpace(ReminderFormTitle))
        {
            ReminderTitleError = ReminderTitleRequiredLabel;
            return;
        }

        if (!_adapter!.TryParseSmartBsDate(ReminderFormDate, out int bsY, out int bsM, out int bsD))
        {
            ReminderDateError = ReminderDateInvalidLabel;
            HasReminderDateError = true;
            return;
        }

        if (!TryParseTime12(ReminderFormTime, ReminderFormIsAm, out string time24))
        {
            ReminderTimeError = ReminderTimeInvalidLabel;
            HasReminderTimeError = true;
            return;
        }

        var recurrence = (ReminderRecurrence)ReminderFormRecurrenceIndex;
        if (recurrence == ReminderRecurrence.None)
        {
            var adDate = _adapter.BsToAd(bsY, bsM, bsD);
            if (adDate is not null && adDate.Value.Date < DateTime.Now.Date)
            {
                ReminderDateError = ReminderDatePastLabel;
                HasReminderDateError = true;
                return;
            }
        }

        string? endDate = string.IsNullOrWhiteSpace(ReminderFormEndDate) ? null : ReminderFormEndDate.Trim();
        if (endDate is not null)
        {
            if (!_adapter.TryParseSmartBsDate(endDate, out int endY, out int endM, out int endD))
            {
                ReminderEndDateError = ReminderEndDateInvalidLabel;
                HasReminderEndDateError = true;
                return;
            }
            var startAd = _adapter.BsToAd(bsY, bsM, bsD);
            var endAd   = _adapter.BsToAd(endY, endM, endD);
            if (startAd is not null && endAd is not null && endAd.Value.Date < startAd.Value.Date)
            {
                ReminderEndDateError = ReminderEndDateBeforeStartLabel;
                HasReminderEndDateError = true;
                return;
            }
        }

        HasReminderDateError = false;
        HasReminderTimeError = false;
        HasReminderEndDateError = false;
        ReminderTitleError = ReminderDateError = ReminderTimeError = ReminderEndDateError = string.Empty;

        if (_editingReminderId is null)
        {
            var entry = new ReminderEntry
            {
                Title      = ReminderFormTitle.Trim(),
                Notes      = ReminderFormNotes,
                BsDate     = ReminderEntry.FormatDate(bsY, bsM, bsD),
                OriginalBsDate = ReminderEntry.FormatDate(bsY, bsM, bsD),
                Time       = time24,
                Recurrence = recurrence,
                EndDate    = endDate,
                CreatedUtc = DateTime.UtcNow,
            };
            _reminderService.Add(entry);
            Log.Action($"reminder added: {entry.Title} on {entry.BsDate}");
        }
        else
        {
            var existing = _reminderService.GetAll().FirstOrDefault(r => r.Id == _editingReminderId);
            if (existing is not null)
            {
                existing.Title      = ReminderFormTitle.Trim();
                existing.Notes      = ReminderFormNotes;
                existing.BsDate     = ReminderEntry.FormatDate(bsY, bsM, bsD);
                existing.Time       = time24;
                existing.Recurrence = recurrence;
                existing.EndDate    = endDate;
                _reminderService.Update(existing);
                Log.Action($"reminder updated: {existing.Title}");
            }
        }

        IsReminderFormOpen = false;
        _editingReminderId = null;
        RefreshReminders();
    }

    public bool HasNotes     => Notes.Count > 0;
    public bool HasReminders => Reminders.Count > 0;
    public bool HasDocuments => Documents.Count > 0;

    public void RefreshDocuments()
    {
        Documents.Clear();
        if (_documentService is null) return;
        foreach (var d in _documentService.GetAll().OrderBy(d => d.Title, StringComparer.CurrentCulture))
            Documents.Add(new DocItemViewModel(d));
        OnPropertyChanged(nameof(HasDocuments));
        UpdateFilteredDocuments();
    }

    private void UpdateFilteredDocuments()
    {
        _filteredDocuments = ComputeFilteredDocuments();
        OnPropertyChanged(nameof(FilteredDocuments));
        OnPropertyChanged(nameof(HasFilteredDocuments));
        OnPropertyChanged(nameof(ShowDocEmpty));
        OnPropertyChanged(nameof(ShowDocNoResults));
    }

    private IReadOnlyList<DocItemViewModel> ComputeFilteredDocuments()
    {
        if (!IsDocSearchActive) return Documents.ToList();

        var text = DocSearchText.Trim();

        if (text.StartsWith('#'))
        {
            var tagQuery = text[1..].ToLowerInvariant();
            return Documents
                .Where(d => d.Tags.Any(t => t.Contains(tagQuery, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        var q = text.ToLowerInvariant();
        var titleMatches = Documents.Where(d => d.Title.Contains(q, StringComparison.OrdinalIgnoreCase));
        var notesMatches = Documents.Where(d => !d.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                                             && d.Notes.Contains(q, StringComparison.OrdinalIgnoreCase));
        var tagMatches   = Documents.Where(d => !d.Title.Contains(q, StringComparison.OrdinalIgnoreCase)
                                             && !d.Notes.Contains(q, StringComparison.OrdinalIgnoreCase)
                                             && d.Tags.Any(t => t.Contains(q, StringComparison.OrdinalIgnoreCase)));
        return titleMatches.Concat(notesMatches).Concat(tagMatches).ToList();
    }

    // ── Search history ────────────────────────────────────────────────────────

    private void UpdateSearchSuggestions()
    {
        DocSearchSuggestions.Clear();
        IsDocSuggestionsOpen = false;
    }

    private void DoDocSearchGotFocus()
    {
        _isSearchBoxFocused = true;
        UpdateSearchSuggestions();
    }

    private void DoDocSearchLostFocus()
    {
        _isSearchBoxFocused = false;
        IsDocSuggestionsOpen = false;
        CommitDocSearch();
    }

    private void DoSelectDocSuggestion(string? term)
    {
        if (term is null) return;
        DocSearchText = term;
        IsDocSuggestionsOpen = false;
    }

    private void CommitDocSearch()
    {
    }

    private void DoClearDocSearch()
    {
        CommitDocSearch();
        DocSearchText = string.Empty;
        IsDocSuggestionsOpen = false;
    }

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
        var item = Notes.FirstOrDefault(n => n.DateKey == dateKey);
        if (item is null) return;
        // Open the inline form pre-filled
        DoCancelNoteForm();
        _editingNoteKey = dateKey;
        _noteFormDateInput = dateKey.Replace('-', '/');
        OnPropertyChanged(nameof(NoteFormDateInput));
        _noteFormText = item.Text;
        OnPropertyChanged(nameof(NoteFormText));
        NoteFormError = string.Empty;
        OnPropertyChanged(nameof(NoteFormTitleLabel));
        IsNoteFormOpen = true;
    }

    private void DoSaveNote()
    {
        if (_inlineEditingNoteKey is null || _notesService is null) return;
        _notesService.SetNote(_inlineEditingNoteKey, NoteEditBuffer);
        _inlineEditingNoteKey = null;
        NoteEditBuffer = string.Empty;
        RefreshNotes();
    }

    private void DoCancelNoteEdit()
    {
        if (_inlineEditingNoteKey is null) return;
        var item = Notes.FirstOrDefault(n => n.DateKey == _inlineEditingNoteKey);
        if (item is not null) item.IsEditing = false;
        _inlineEditingNoteKey = null;
        NoteEditBuffer = string.Empty;
    }

    // ── Reminder operations ──────────────────────────────────────────────────

    private void DoDeleteReminder(string? id)
    {
        if (id is null || _reminderService is null) return;
        _reminderService.Delete(id);
        RefreshReminders();
    }

    // ── Document operations ───────────────────────────────────────────────────

    private void DoShowAddDocument()
    {
        DoCancelDocumentEdit();
        IsDocFormOpen = true;
    }

    private void DoBrowseDocumentFile()
    {
        var dlg = new Microsoft.Win32.OpenFileDialog
        {
            Title  = "Select Document",
            Filter = "All supported|*.pdf;*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff;*.doc;*.docx;*.xls;*.xlsx;*.txt" +
                     "|PDF Files|*.pdf|Images|*.jpg;*.jpeg;*.png;*.bmp;*.tif;*.tiff" +
                     "|Word Documents|*.doc;*.docx|Excel Files|*.xls;*.xlsx|All Files|*.*",
        };
        if (dlg.ShowDialog() == true)
            DocEditFilePath = dlg.FileName;
    }

    private void DoAddDocTagPreset(string? tag)
    {
        if (tag is null) return;
        if (!DocEditTags.Any(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase)))
            DocEditTags.Add(tag);
    }

    private void DoAddDocTagFromInput()
    {
        if (string.IsNullOrWhiteSpace(DocEditTagInput)) return;
        foreach (var part in DocEditTagInput.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!string.IsNullOrEmpty(part)
                && !DocEditTags.Any(t => string.Equals(t, part, StringComparison.OrdinalIgnoreCase)))
            {
                DocEditTags.Add(part);
            }
        }
        DocEditTagInput = string.Empty;
    }

    private void DoRemoveDocTag(string? tag)
    {
        if (tag is null) return;
        var existing = DocEditTags.FirstOrDefault(t => string.Equals(t, tag, StringComparison.OrdinalIgnoreCase));
        if (existing is not null) DocEditTags.Remove(existing);
    }

    private void DoSaveDocument()
    {
        if (string.IsNullOrWhiteSpace(DocEditTitle))
        {
            DocEditError = DocTitleRequiredLabel;
            return;
        }
        if (string.IsNullOrWhiteSpace(DocEditFilePath))
        {
            DocEditError = DocFileRequiredLabel;
            return;
        }

        var titleTrimmed = DocEditTitle.Trim();

        // Duplicate title check (case-insensitive, excluding current entry on edit)
        var hasDuplicate = _documentService?.GetAll()
            .Any(d => string.Equals(d.Title, titleTrimmed, StringComparison.OrdinalIgnoreCase)
                   && d.Id != _editingDocId) ?? false;
        if (hasDuplicate)
        {
            DocEditError = DocDuplicateTitleLabel;
            return;
        }

        DocEditError = string.Empty;
        DoAddDocTagFromInput();

        try
        {
            if (_editingDocId is null)
            {
                var entry = new DocumentEntry
                {
                    Title  = titleTrimmed,
                    Tags   = DocEditTags.ToList(),
                    Notes  = DocEditNotes.Trim(),
                };
                entry.FilePath = EnsureManaged(titleTrimmed, DocEditFilePath, null);
                _documentService?.Add(entry);
                Log.Action($"document added: {entry.Title}");
            }
            else
            {
                var existing = _documentService?.GetAll().FirstOrDefault(d => d.Id == _editingDocId);
                if (existing is not null)
                {
                    var oldManagedPath = IsManaged(existing.FilePath) ? existing.FilePath : null;
                    existing.Title    = titleTrimmed;
                    existing.Tags     = DocEditTags.ToList();
                    existing.Notes    = DocEditNotes.Trim();
                    existing.FilePath = EnsureManaged(titleTrimmed, DocEditFilePath, oldManagedPath);
                    _documentService?.Update(existing);
                    Log.Action($"document updated: {existing.Title}");
                }
            }
        }
        catch (Exception ex)
        {
            Log.Error("Failed to save document file", ex);
            DocEditError = "File operation failed.";
            return;
        }

        DoCancelDocumentEdit();
        RefreshDocuments();
    }

    private void DoCancelDocumentEdit()
    {
        _editingDocId    = null;
        IsDocFormOpen    = false;
        IsEditingDoc     = false;
        DocEditTitle     = string.Empty;
        DocEditTags.Clear();
        DocEditTagInput  = string.Empty;
        DocEditFilePath  = string.Empty;
        DocEditNotes     = string.Empty;
        DocEditError     = string.Empty;
    }

    private void DoDeleteDocument(string? id)
    {
        if (id is null || _documentService is null) return;
        var entry = _documentService.GetAll().FirstOrDefault(d => d.Id == id);
        if (entry is not null) DeleteManaged(entry.FilePath);
        _documentService.Delete(id);
        RefreshDocuments();
    }

    private void DoEditDocument(string? id)
    {
        if (id is null || _documentService is null) return;
        var entry = _documentService.GetAll().FirstOrDefault(d => d.Id == id);
        if (entry is null) return;
        DoCancelDocumentEdit();
        _editingDocId   = id;
        IsEditingDoc    = true;
        DocEditTitle    = entry.Title;
        foreach (var tag in entry.Tags) DocEditTags.Add(tag);
        DocEditFilePath = entry.FilePath;
        DocEditNotes    = entry.Notes;
        IsDocFormOpen   = true;
    }

    private void DoOpenDocument(string? id)
    {
        if (id is null || _documentService is null) return;
        var entry = _documentService.GetAll().FirstOrDefault(d => d.Id == id);
        if (entry is null || !File.Exists(entry.FilePath)) return;
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(entry.FilePath) { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error("Failed to open document", ex);
        }
    }

    private void DoOpenDocumentFolder(string? id)
    {
        if (id is null || _documentService is null) return;
        var entry = _documentService.GetAll().FirstOrDefault(d => d.Id == id);
        if (entry is null || string.IsNullOrWhiteSpace(entry.FilePath)) return;
        var folder = Path.GetDirectoryName(entry.FilePath);
        if (folder is null || !Directory.Exists(folder)) return;
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo("explorer.exe", $"/select,\"{entry.FilePath}\"")
                { UseShellExecute = true });
        }
        catch (Exception ex)
        {
            Log.Error("Failed to open document folder", ex);
        }
    }

    // ── File management helpers ───────────────────────────────────────────────

    private static bool IsManaged(string path) =>
        !string.IsNullOrWhiteSpace(path)
        && path.StartsWith(AppPaths.DocumentsFilesDirectory, StringComparison.OrdinalIgnoreCase);

    private static string SanitizeTitle(string title)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder();
        foreach (var c in title.Trim())
            sb.Append(invalid.Contains(c) ? '_' : c);
        var result = sb.ToString().Trim(' ', '.');
        return string.IsNullOrEmpty(result) ? "document" : result;
    }

    private static string EnsureManaged(string title, string sourcePath, string? currentManagedPath)
    {
        Directory.CreateDirectory(AppPaths.DocumentsFilesDirectory);
        var ext  = Path.GetExtension(sourcePath);
        var dest = Path.Combine(AppPaths.DocumentsFilesDirectory, SanitizeTitle(title) + ext);

        if (IsManaged(sourcePath))
        {
            // File already in managed folder - rename/move if title changed
            if (!string.Equals(sourcePath, dest, StringComparison.OrdinalIgnoreCase))
                File.Move(sourcePath, dest, overwrite: true);
        }
        else
        {
            // User picked a new file from outside managed folder
            if (currentManagedPath is not null && File.Exists(currentManagedPath))
                File.Delete(currentManagedPath);
            File.Copy(sourcePath, dest, overwrite: true);
        }

        return dest;
    }

    private static void DeleteManaged(string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(filePath) && IsManaged(filePath) && File.Exists(filePath))
            File.Delete(filePath);
    }
}

// ── Item ViewModels ───────────────────────────────────────────────────────────

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
        Id    = entry.Id;
        Title = entry.Title;
        Date  = entry.BsDate;
        Time  = entry.Time;
        Notes = entry.Notes;
        IsCompleted = entry.IsCompleted;
        RecurrenceText = entry.Recurrence switch
        {
            ReminderRecurrence.Daily   => "Daily",
            ReminderRecurrence.Weekly  => "Weekly",
            ReminderRecurrence.Monthly => "Monthly",
            ReminderRecurrence.Yearly  => "Yearly",
            _ => string.Empty,
        };
    }
}

public sealed class DocItemViewModel : ViewModelBase
{
    public string Id       { get; }
    public string Title    { get; }
    public IReadOnlyList<string> Tags { get; }
    public string FilePath { get; }
    public string Notes    { get; }

    public bool HasTags     => Tags.Count > 0;
    public bool HasNotes    => !string.IsNullOrWhiteSpace(Notes);
    public bool HasFile     => !string.IsNullOrWhiteSpace(FilePath);
    public bool HasFileOrTags => HasFile || HasTags;
    public bool FileExists  => HasFile && File.Exists(FilePath);
    public bool FileNotFound => HasFile && !File.Exists(FilePath);
    public string FileName      => HasFile ? Path.GetFileName(FilePath) : string.Empty;
    public string FileExtension => HasFile ? Path.GetExtension(FilePath).TrimStart('.').ToUpperInvariant() : string.Empty;

    public DocItemViewModel(DocumentEntry entry)
    {
        Id       = entry.Id;
        Title    = entry.Title;
        Tags     = entry.Tags.AsReadOnly();
        FilePath = entry.FilePath;
        Notes    = entry.Notes;
    }
}
