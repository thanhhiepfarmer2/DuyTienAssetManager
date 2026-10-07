using DuyTien.AssetManagement.Application.Authentication;

namespace DuyTien.AssetManagement.ArchitectureTests;

public sealed class AuthenticationPolicyTests
{
    [Theory]
    [InlineData("1234567")]
    [InlineData("")]
    public void Password_policy_rejects_only_values_shorter_than_eight(string password)
    {
        Assert.False(AuthPasswordPolicy.IsValid(password));
    }

    [Theory]
    [InlineData("12345678")]
    [InlineData("aaaaaaaa")]
    [InlineData("username")]
    [InlineData("        ")]
    [InlineData("mật khẩu rất dài không có chữ số")]
    public void Password_policy_has_no_other_content_or_maximum_rule(string password)
    {
        Assert.True(AuthPasswordPolicy.IsValid(password));
    }

    [Fact]
    public void Location_hierarchy_uses_parent_id_for_descendants()
    {
        var hierarchy = ScopeHierarchy.Create(
            AuthScopeType.Location,
            [
                new ScopeNode("LOC-DEMO-A", null),
                new ScopeNode("LOC-DEMO-A-CHILD", "LOC-DEMO-A"),
                new ScopeNode("LOC-DEMO-B", null)
            ]);

        Assert.True(hierarchy.IsWithin("LOC-DEMO-A", "LOC-DEMO-A-CHILD", true));
        Assert.False(hierarchy.IsWithin("LOC-DEMO-A", "LOC-DEMO-A-CHILD", false));
        Assert.False(hierarchy.IsWithin("LOC-DEMO-A", "LOC-DEMO-B", true));
    }

    [Fact]
    public void Location_hierarchy_rejects_missing_parent_and_cycle()
    {
        Assert.Throws<AuthValidationException>(() => ScopeHierarchy.Create(
            AuthScopeType.Location,
            [new ScopeNode("LOC-CHILD", "LOC-MISSING")]));

        Assert.Throws<AuthValidationException>(() => ScopeHierarchy.Create(
            AuthScopeType.Location,
            [
                new ScopeNode("LOC-A", "LOC-B"),
                new ScopeNode("LOC-B", "LOC-A")
            ]));
    }
}
