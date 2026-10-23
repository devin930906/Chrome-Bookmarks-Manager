using System.Reflection;

namespace ChromeBookmarksManager.Tests.Ui;

public sealed class AppMessageDialogPolicyTests
{
    [Theory]
    [InlineData("YesNoCancel", "Cancel")]
    [InlineData("OKCancel", "Cancel")]
    [InlineData("YesNo", "No")]
    [InlineData("OK", "OK")]
    public void Cancellation_result_is_safe_for_each_button_set(string buttonSet, string expected)
    {
        var result = InvokePolicy("GetCancellationResult", buttonSet);

        Assert.Equal(expected, result.ToString());
    }

    [Theory]
    [InlineData("YesNoCancel", "None", "Cancel")]
    [InlineData("OKCancel", "None", "Cancel")]
    [InlineData("YesNo", "None", "No")]
    [InlineData("OK", "None", "OK")]
    [InlineData("YesNoCancel", "Yes", "Yes")]
    [InlineData("YesNoCancel", "No", "No")]
    [InlineData("YesNoCancel", "Cancel", "Cancel")]
    [InlineData("OKCancel", "OK", "OK")]
    [InlineData("OKCancel", "Cancel", "Cancel")]
    [InlineData("YesNo", "Yes", "Yes")]
    [InlineData("YesNo", "No", "No")]
    public void Default_result_preserves_valid_defaults_and_uses_safe_fallbacks(
        string buttonSet,
        string requestedDefault,
        string expected)
    {
        var result = InvokePolicy("GetDefaultResult", buttonSet, requestedDefault);

        Assert.Equal(expected, result.ToString());
    }

    [Theory]
    [InlineData("YesNoCancel", "OK", "Cancel")]
    [InlineData("OKCancel", "Yes", "Cancel")]
    [InlineData("YesNo", "Cancel", "No")]
    [InlineData("OK", "No", "OK")]
    public void Default_result_rejects_results_absent_from_the_button_set(
        string buttonSet,
        string requestedDefault,
        string expected)
    {
        var result = InvokePolicy("GetDefaultResult", buttonSet, requestedDefault);

        Assert.Equal(expected, result.ToString());
    }

    [Theory]
    [InlineData("YesNoCancel", "Yes", "Yes", "Cancel")]
    [InlineData("OKCancel", "OK", "OK", "Cancel")]
    [InlineData("YesNo", "Yes", "Yes", "No")]
    [InlineData("OK", "OK", "OK", "OK")]
    public void Closing_without_an_explicit_button_uses_cancellation_instead_of_the_default(
        string buttonSet,
        string requestedDefault,
        string expectedDefault,
        string expectedCloseResult)
    {
        var actualDefault = InvokePolicy("GetDefaultResult", buttonSet, requestedDefault);
        var closeResult = InvokePolicy("GetResultOnClose", buttonSet, "None");

        Assert.Equal(expectedDefault, actualDefault.ToString());
        Assert.Equal(expectedCloseResult, closeResult.ToString());
    }

    [Theory]
    [InlineData("YesNoCancel", "Yes", "Yes")]
    [InlineData("YesNoCancel", "No", "No")]
    [InlineData("YesNoCancel", "Cancel", "Cancel")]
    [InlineData("OK", "OK", "OK")]
    public void Closing_after_an_explicit_button_result_preserves_that_result(
        string buttonSet,
        string explicitResult,
        string expected)
    {
        var result = InvokePolicy("GetResultOnClose", buttonSet, explicitResult);

        Assert.Equal(expected, result.ToString());
    }

    [Theory]
    [InlineData("false", "0")]
    [InlineData("true", "1")]
    public void Message_column_is_the_star_column_with_or_without_an_icon(string hasIcon, string expectedColumn)
    {
        var result = InvokePolicy("GetMessageColumn", hasIcon);

        Assert.Equal(expectedColumn, result.ToString());
    }

    [Fact]
    public void Show_api_matches_the_existing_message_box_call_shape()
    {
        var type = GetDialogType();
        var method = type.GetMethod("Show", BindingFlags.Public | BindingFlags.Static);

        Assert.NotNull(method);
        Assert.Equal("System.Windows.MessageBoxResult", method!.ReturnType.FullName);

        var parameters = method.GetParameters();
        Assert.Equal(6, parameters.Length);
        Assert.Equal("System.Windows.Window", parameters[0].ParameterType.FullName);
        Assert.Equal(typeof(string), parameters[1].ParameterType);
        Assert.Equal(typeof(string), parameters[2].ParameterType);
        Assert.Equal("System.Windows.MessageBoxButton", parameters[3].ParameterType.FullName);
        Assert.Equal("System.Windows.MessageBoxImage", parameters[4].ParameterType.FullName);
        Assert.Equal("System.Windows.MessageBoxResult", parameters[5].ParameterType.FullName);
        Assert.True(parameters[3].IsOptional);
        Assert.True(parameters[4].IsOptional);
        Assert.True(parameters[5].IsOptional);
        Assert.Equal("OK", parameters[3].DefaultValue!.ToString());
        Assert.Equal("None", parameters[4].DefaultValue!.ToString());
        Assert.Equal("None", parameters[5].DefaultValue!.ToString());

        Assert.NotNull(type.GetMethod("ShowSaveChanges", BindingFlags.Public | BindingFlags.Static));
    }

    private static object InvokePolicy(string methodName, params string[] values)
    {
        var method = GetDialogType().GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            .SingleOrDefault(candidate => candidate.Name == methodName);
        Assert.NotNull(method);

        var parameters = method!.GetParameters();
        Assert.Equal(parameters.Length, values.Length);

        var arguments = new object[parameters.Length];
        for (var index = 0; index < parameters.Length; index++)
        {
            var parameterType = parameters[index].ParameterType;
            arguments[index] = parameterType.IsEnum
                ? Enum.Parse(parameterType, values[index])
                : Convert.ChangeType(values[index], parameterType);
        }

        return method.Invoke(null, arguments)!;
    }

    private static Type GetDialogType()
    {
        var assembly = Assembly.Load("ChromeBookmarksManager");
        var type = assembly.GetType("ChromeBookmarksManager.AppMessageDialog", throwOnError: false);
        Assert.NotNull(type);
        return type!;
    }
}
