namespace UIFramework;

/// <summary>Optional modules prepare data before business initialization and bind after it.</summary>
public interface IUINodeExtension
{
    void BeforeInitialize(UINode node);
    void AfterInitialize(UINode node);
    ValueTask DisposeAsync(UINode node);
}
