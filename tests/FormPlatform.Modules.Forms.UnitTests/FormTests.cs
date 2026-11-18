using FormPlatform.Modules.Forms.Domain;
using Xunit;

namespace FormPlatform.Modules.Forms.UnitTests;

public sealed class FormTests
{
	private static readonly DateTime Now = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

	[Fact]
	public void Creates_form_with_valid_data()
	{
		var form = Form.Create(Guid.NewGuid(), " contact ", "Contact", null, FormStatus.Draft, Now);
		Assert.Equal("contact", form.Code);
	}

	[Fact]
	public void Rejects_missing_tenant() => Assert.Throws<ArgumentException>(() => Form.Create(Guid.Empty, "x", "X", null, FormStatus.Draft, Now));

	[Fact]
	public void Rejects_empty_code() => Assert.Throws<ArgumentException>(() => Form.Create(Guid.NewGuid(), " ", "X", null, FormStatus.Draft, Now));

	[Fact]
	public void Creates_sequential_draft_versions()
	{
		var form = CreateForm();
		Assert.Equal(new[] { 1, 2 }, new[] { form.CreateDraftVersion(Now).VersionNumber, form.CreateDraftVersion(Now).VersionNumber });
	}

	[Fact]
	public void Draft_schema_can_be_modified()
	{
		var version = CreateForm().CreateDraftVersion(Now);
		version.UpdateSchema(ValidSchema);
		Assert.Contains("fields", version.Schema);	
	}

	[Fact]
	public void Published_version_is_immutable_and_cannot_be_published_again()
	{
		var version = CreateForm().CreateDraftVersion(Now);
		version.Publish(Now);
		Assert.Equal(Now, version.PublishedAtUtc);
		Assert.Throws<InvalidOperationException>(() => version.UpdateSchema(ValidSchema));
		Assert.Throws<InvalidOperationException>(() => version.Publish(Now.AddMinutes(1)));
		Assert.Equal(Now, version.PublishedAtUtc);
	}

	[Fact]
	public void Retired_version_is_immutable_and_retains_its_publication_time()
	{
		var version = CreateForm().CreateDraftVersion(Now);
		version.Publish(Now);
		version.Retire();
		Assert.Equal(FormVersionStatus.Retired, version.Status);
		Assert.Equal(Now, version.PublishedAtUtc);
		Assert.Throws<InvalidOperationException>(() => version.UpdateSchema(ValidSchema));
		Assert.Throws<InvalidOperationException>(() => version.Publish(Now.AddMinutes(1)));
		Assert.Throws<InvalidOperationException>(() => version.Retire());
	}

	[Fact]
	public void Draft_cannot_be_retired()
	{
		var version = CreateForm().CreateDraftVersion(Now);
		Assert.Throws<InvalidOperationException>(() => version.Retire());
		Assert.Null(version.PublishedAtUtc);
	}

	[Theory]
	[InlineData("not json")]
	[InlineData("null")]
	[InlineData("[]")]
	[InlineData("{\"metadata\":null,\"fields\":[]}")]
	[InlineData("{\"metadata\":\"text\",\"fields\":[]}")]
	[InlineData("{\"metadata\":{},\"fields\":null}")]
	[InlineData("{\"metadata\":{},\"fields\":{}}")]
	[InlineData("{\"metadata\":{},\"fields\":[null]}")]
	[InlineData("{\"metadata\":{},\"fields\":[{}]}")]
	[InlineData("{\"metadata\":{},\"fields\":[{\"id\":\" \",\"type\":\"text\",\"name\":\"Name\",\"config\":{}}]}")]
	[InlineData("{\"metadata\":{},\"fields\":[{\"id\":\"id\",\"type\":\" \",\"name\":\"Name\",\"config\":{}}]}")]
	[InlineData("{\"metadata\":{},\"fields\":[{\"id\":\"id\",\"type\":\"text\",\"name\":\" \",\"config\":{}}]}")]
	[InlineData("{\"metadata\":{},\"fields\":[{\"id\":\"id\",\"type\":\"text\",\"name\":\"Name\",\"config\":null}]}")]
	[InlineData("{\"metadata\":{},\"fields\":[{\"id\":\"id\",\"type\":\"text\",\"name\":\"Name\",\"config\":[]}]}")]
	[InlineData("{\"metadata\":{},\"fields\":[{\"id\":\"id\",\"type\":\"text\",\"name\":\"Name\",\"config\":{}},{\"id\":\"id\",\"type\":\"text\",\"name\":\"Other\",\"config\":{}}]}")]
	public void Rejects_invalid_schema(string schema) => Assert.False(SchemaDocument.IsValid(schema));

	[Fact]
	public void Accepts_empty_and_populated_schema()
	{
		Assert.True(SchemaDocument.IsValid("{\"metadata\":{},\"fields\":[]}"));
		Assert.True(SchemaDocument.IsValid(ValidSchema));
		Assert.True(SchemaDocument.IsValid("{\"metadata\":{},\"fields\":[{\"id\":\"name\",\"type\":\"text\",\"name\":\"Name\",\"configuration\":{}}]}"));
	}

	private const string ValidSchema = "{\"metadata\":{},\"fields\":[{\"id\":\"name\",\"type\":\"text\",\"name\":\"Name\",\"configuration\":{}}]}";

	private static Form CreateForm() => Form.Create(Guid.NewGuid(), "contact", "Contact", null, FormStatus.Draft, Now);
}
