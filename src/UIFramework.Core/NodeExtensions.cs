namespace UIFramework;

/// <summary>Optional modules prepare data before business initialization and bind after it.</summary>
public interface IUINodeExtension
{
    void BeforeInitialize(UINode node);
    Task AfterInitializeAsync(UINode node, CancellationToken token);
    ValueTask DisposeAsync(UINode node);
}
