using NepDateWidget.Services;

namespace NepDateWidget.Tests.Services;

/// <summary>
/// Fake IAutoStartService that stores the state in-memory.
/// Used by MainViewModelTests to avoid touching the Windows registry.
/// </summary>
public sealed class FakeAutoStartService : IAutoStartService
{
    public bool IsEnabled        { get; private set; }
    public int  SetEnabledCount  { get; private set; }

    public FakeAutoStartService(bool initialState = false)
    {
        IsEnabled = initialState;
    }

    public void SetEnabled(bool enable)
    {
        IsEnabled = enable;
        SetEnabledCount++;
    }

    public int RefreshIfStaleCount { get; private set; }
    public void RefreshIfStale() => RefreshIfStaleCount++;
}

/// <summary>
/// Tests for AutoStartService behavior contract via the interface.
/// Does NOT test actual registry interaction to keep tests environment-independent.
/// The contract tests verify the fake behaves as expected by consumers.
/// </summary>
public class AutoStartServiceContractTests
{
    [Fact]
    public void FakeAutoStart_InitialState_False_ByDefault()
    {
        var svc = new FakeAutoStartService();
        Assert.False(svc.IsEnabled);
    }

    [Fact]
    public void FakeAutoStart_InitialState_True_WhenSeeded()
    {
        var svc = new FakeAutoStartService(initialState: true);
        Assert.True(svc.IsEnabled);
    }

    [Fact]
    public void FakeAutoStart_SetEnabled_True_SetsIsEnabled()
    {
        var svc = new FakeAutoStartService();
        svc.SetEnabled(true);
        Assert.True(svc.IsEnabled);
    }

    [Fact]
    public void FakeAutoStart_SetEnabled_False_ClearsIsEnabled()
    {
        var svc = new FakeAutoStartService(initialState: true);
        svc.SetEnabled(false);
        Assert.False(svc.IsEnabled);
    }

    [Fact]
    public void FakeAutoStart_SetEnabled_TracksCalls()
    {
        var svc = new FakeAutoStartService();
        svc.SetEnabled(true);
        svc.SetEnabled(false);
        Assert.Equal(2, svc.SetEnabledCount);
    }

    [Fact]
    public void AutoStartService_DoesNotThrow_OnSetEnabled()
    {
        // Integration smoke test: verify the real service doesn't throw on toggle.
        // We immediately restore the state to avoid leaving a registry entry.
        var svc     = new AutoStartService();
        bool before = svc.IsEnabled;
        var ex = Record.Exception(() =>
        {
            svc.SetEnabled(!before);
            svc.SetEnabled(before);  // restore
        });
        Assert.Null(ex);
    }
}
