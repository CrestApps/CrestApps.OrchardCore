using System.Diagnostics;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace CrestApps.OrchardCore.Tests.Architecture;

/// <summary>
/// Requires every data migration to be additive unless a destructive step carries a checkable justification.
/// </summary>
/// <remarks>
/// A rolling upgrade runs old and new code against one schema at the same time. A migration that drops or renames
/// a column, index, or table makes the still-running old node fail the moment it touches the changed object, so
/// the upgrade stops being rolling and becomes an outage. The safe shape is expand and contract: add in one
/// release, stop reading it in that release, and remove only in a later one, once no supported version reads it.
/// <para>
/// Destructive steps are found through three oracles, because each is blind to what the others see. The first
/// reads the syntax tree and matches schema-builder invocations such as <c>DropColumn</c> — the mechanism most
/// migrations use. The second and third govern raw SQL, which carries no recognizable method name at all: one
/// covers statements passed as an argument, and one covers statements assigned to a command's text, which is the
/// dominant raw-SQL shape in this repository and which an argument-only oracle would miss entirely.
/// </para>
/// <para>
/// The raw-SQL oracles deliberately do not scan string literals in isolation. A literal scan is defeated by
/// ordinary C#: <c>"drop " + "table " + name</c> and <c>$"drop {kind} {name}"</c> are both destructive and neither
/// contains a literal that matches. Instead the statement is reconstructed from the syntax tree across
/// concatenation and interpolation, with each runtime hole replaced by a placeholder, and then classified by its
/// leading verb. A reconstruction that begins with a destructive verb is an occurrence even when the object it
/// names is computed, and a reconstruction whose leading verb cannot be determined is also an occurrence:
/// migrations may not execute SQL this gate cannot read. Where a reviewed helper legitimately builds its statement
/// dynamically, that is recorded in the register as a deliberate, justified decision rather than silently allowed.
/// </para>
/// <para>
/// Attribution is syntactic rather than textual. Each occurrence is bound to its enclosing method declaration, so
/// a register entry names the method that actually performs the change even when that is a private helper several
/// calls below the <c>UpdateFromNAsync</c> step. A line-proximity heuristic would mis-attribute exactly those
/// cases, which are the ones most likely to hide a destructive step.
/// </para>
/// <para>
/// <c>UninstallAsync</c> is exempt, and only <c>UninstallAsync</c>. Orchard runs it when a feature is being
/// removed entirely, which is not an upgrade path and has no old node left to break; requiring an expand/contract
/// window there would forbid feature uninstall from cleaning up after itself.
/// </para>
/// <para>
/// Every other occurrence needs a register entry, and an entry authorizes one operation against one named object.
/// Registering a method rather than an occurrence would turn each entry into a standing bypass of everything that
/// method might later do, so an entry must match exactly one occurrence: matching none means it is stale, and
/// matching several means the code broadened underneath it.
/// </para>
/// <para>
/// Justifications are machine-checked rather than prose. A <see cref="MigrationContractJustification.ContractPhase"/>
/// entry must name the strictly older version that performed the expand, which mechanically prevents expand and
/// contract landing in the same release — the failure mode the whole discipline exists to prevent. A
/// <see cref="MigrationContractJustification.NeverReleased"/> entry claims the object never reached a customer,
/// and that claim is checked against the repository's own release tags rather than trusted: it fails if any stable
/// release at or above the named version was ever tagged, and it fails closed when the release history is not
/// available. Prerelease tags are excluded deliberately, because upgrading from a preview or release candidate is
/// not a supported path; that is a stated boundary of this gate rather than an oversight. A
/// <see cref="MigrationContractJustification.ReviewedAdditive"/> entry covers a statement the gate cannot read but
/// a human has confirmed is additive, and it is pinned to the exact call site so any change to that call site
/// invalidates it.
/// </para>
/// <para>
/// The scan itself is path-based, which is fast and needs no compilation but is only correct while every migration
/// lives under a <c>Migrations</c> folder. Orchard finds migrations by registration rather than by path, so that
/// convention is verified rather than assumed: every type passed to <c>AddDataMigration</c> must appear in the
/// scanned surface.
/// </para>
/// </remarks>
public sealed class MigrationAdditiveOnlyGuardTests
{
    private const string UninstallMethodName = "UninstallAsync";
    private const string CommandTextMemberName = "CommandText";
    private const char RuntimeHolePlaceholder = '\u0001';
    private const int MaximumLocalResolutionDepth = 4;
    private const string UndeterminableTarget = "(undeterminable)";
    private const string QueryBuilderTypeName = "SqlBuilder";

    private const string QueryBuilderTypeFullName = "YesSql.Sql.SqlBuilder";

    private static readonly HashSet<string> _destructiveSchemaOperations = new(StringComparer.Ordinal)
    {
        "AlterColumn",
        "DropColumn",
        "DropForeignKey",
        "DropForeignKeyAsync",
        "DropIndex",
        "DropMapIndexTable",
        "DropMapIndexTableAsync",
        "DropReduceIndexTable",
        "DropReduceIndexTableAsync",
        "DropSchema",
        "DropSchemaAsync",
        "DropTable",
        "DropTableAsync",
        "RenameColumn",
        "RenameTable",

    };

    private static readonly HashSet<string> _sqlExecutionOperations = new(StringComparer.Ordinal)
    {
        "Execute",
        "ExecuteAsync",
        "ExecuteNonQuery",
        "ExecuteNonQueryAsync",
        "ExecuteReader",
        "ExecuteReaderAsync",
        "ExecuteScalar",
        "ExecuteScalarAsync",
        "Query",
        "QueryAsync",
        "QueryFirst",
        "QueryFirstAsync",
        "QueryFirstOrDefault",
        "QueryFirstOrDefaultAsync",
        "QuerySingle",
        "QuerySingleAsync",
        "QuerySingleOrDefault",
        "QuerySingleOrDefaultAsync",
    };

    private static readonly HashSet<string> _readOnlySqlBuilderOperations = new(StringComparer.Ordinal)
    {
        "AddSelector",
        "Distinct",
        "From",
        "GroupBy",
        "Having",
        "InnerJoin",
        "Join",
        "LeftJoin",
        "OrderBy",
        "OrderByDescending",
        "Selector",
        "Skip",
        "Take",
        "ThenOrderBy",
        "ThenOrderByDescending",
        "ToSqlString",
        "Where",
        "WhereAnd",
        "WhereOr",
    };

    private static readonly HashSet<string> _destructiveSqlVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "alter",
        "delete",
        "drop",
        "truncate",
    };

    private static readonly Regex _stableReleaseTagRegex = new(
        @"^v?(?<version>\d+\.\d+\.\d+)$",
        RegexOptions.Compiled);

    private static readonly Regex _identifierQuotingRegex = new(
        "[\"`\\[\\]]",
        RegexOptions.Compiled);

    // A single destructive statement can name several objects, so the operand is a comma-separated list rather than one
    // identifier. Reading only the first would let "drop table Authorized, Unauthorized" bind to the authorized name.
    private const string OperandListPattern = @"[^\s;(,]+(?:\s*,\s*[^\s;(,]+)*";

    private static readonly Regex _destructiveOperandRegex = new(
        @"\b(?:drop|truncate)\s+(?:table|index|view|materialized\s+view)\s+(?:if\s+exists\s+)?(?<object>" + OperandListPattern + ")"
        + @"|\balter\s+table\s+(?<object>" + OperandListPattern + ")"
        + @"|\bdelete\s+from\s+(?<object>" + OperandListPattern + ")",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Each operation is paired with the type that must declare it. The name is not the evidence: reconstruction only
    // passes through a call whose resolved symbol belongs to the data layer's own abstraction, so a look-alike member on
    // any other type is refused instead of being trusted to preserve the object's identity.
    private static readonly Dictionary<string, string> _identityPreservingSqlOperations = new(StringComparer.Ordinal)
    {
        ["GetDocumentTable"] = "YesSql.ITableNameConvention",
        ["GetIndexTable"] = "YesSql.ITableNameConvention",
        ["QuoteForColumnName"] = "YesSql.ISqlDialect",
        ["QuoteForTableName"] = "YesSql.ISqlDialect",
    };

    private static readonly Lazy<MetadataReference[]> _metadataReferences = new(LoadMetadataReferences);

    // Migration sources rely on the SDK's implicit global usings. Without them nothing binds, every call is refused, and
    // the gate fails for a reason that has nothing to do with what the migrations do.
    private static readonly Lazy<SyntaxTree> _implicitUsings = new(() => CSharpSyntaxTree.ParseText("""
        global using System;
        global using System.Collections.Generic;
        global using System.IO;
        global using System.Linq;
        global using System.Net.Http;
        global using System.Threading;
        global using System.Threading.Tasks;
        global using Microsoft.AspNetCore.Builder;
        global using Microsoft.AspNetCore.Http;
        global using Microsoft.Extensions.Configuration;
        global using Microsoft.Extensions.DependencyInjection;
        global using Microsoft.Extensions.Hosting;
        global using Microsoft.Extensions.Logging;
        """));

    private static readonly HashSet<string> _dynamicSqlExecutionVerbs = new(StringComparer.OrdinalIgnoreCase)
    {
        "begin",
        "do",
        "exec",
        "execute",
        "sp_executesql",
    };

    private static readonly Regex _dataMigrationRegistrationRegex = new(
        @"AddDataMigration<\s*(?<type>[A-Za-z0-9_]+)\s*>",
        RegexOptions.Compiled);

    private static readonly MigrationContractEntry[] _authorizedContractSteps =
    [
        new MigrationContractEntry(
            "src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Migrations/OmnichannelActivityIndexMigrations.cs",
            "OmnichannelActivityIndexMigrations",
            "UpdateFrom3Async",
            "DropColumn",
            "AssignedToUsername",
            MigrationContractJustification.NeverReleased,
            "2.0.0",
            "AssignedToUsername",
            "Denormalized display column that only ever existed inside the unreleased 2.0.0 line; no stable release ever shipped it, so no supported upgrade path reads it."),
        new MigrationContractEntry(
            "src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Migrations/OmnichannelActivityIndexMigrations.cs",
            "OmnichannelActivityIndexMigrations",
            "UpdateFrom3Async",
            "DropColumn",
            "CreatedByUsername",
            MigrationContractJustification.NeverReleased,
            "2.0.0",
            "CreatedByUsername",
            "Denormalized display column that only ever existed inside the unreleased 2.0.0 line; no stable release ever shipped it, so no supported upgrade path reads it."),
        new MigrationContractEntry(
            "src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Migrations/OmnichannelContactsMigrations.cs",
            "OmnichannelContactsMigrations",
            "RemoveRedundantNationalPhoneColumnsAsync",
            "DropIndex",
            "IDX_OCIndex_NationalCell",
            MigrationContractJustification.NeverReleased,
            "2.0.0",
            "IDX_OCIndex_NationalCell",
            "Index over a redundant normalized-phone column introduced and removed inside the unreleased 2.0.0 line."),
        new MigrationContractEntry(
            "src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Migrations/OmnichannelContactsMigrations.cs",
            "OmnichannelContactsMigrations",
            "RemoveRedundantNationalPhoneColumnsAsync",
            "DropIndex",
            "IDX_OCIndex_NationalHome",
            MigrationContractJustification.NeverReleased,
            "2.0.0",
            "IDX_OCIndex_NationalHome",
            "Index over a redundant normalized-phone column introduced and removed inside the unreleased 2.0.0 line."),
        new MigrationContractEntry(
            "src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Migrations/OmnichannelContactsMigrations.cs",
            "OmnichannelContactsMigrations",
            "RemoveRedundantNationalPhoneColumnsAsync",
            "DropColumn",
            "NationalPrimaryCellPhoneNumber",
            MigrationContractJustification.NeverReleased,
            "2.0.0",
            "NationalPrimaryCellPhoneNumber",
            "Redundant normalized-phone column introduced and removed inside the unreleased 2.0.0 line; the value is recomputed on read."),
        new MigrationContractEntry(
            "src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Migrations/OmnichannelContactsMigrations.cs",
            "OmnichannelContactsMigrations",
            "RemoveRedundantNationalPhoneColumnsAsync",
            "DropColumn",
            "NationalPrimaryHomePhoneNumber",
            MigrationContractJustification.NeverReleased,
            "2.0.0",
            "NationalPrimaryHomePhoneNumber",
            "Redundant normalized-phone column introduced and removed inside the unreleased 2.0.0 line; the value is recomputed on read."),
        new MigrationContractEntry(
            "src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Migrations/OmnichannelContactsMigrations.cs",
            "OmnichannelContactsMigrations",
            "DropLegacyPhoneIndexTableAsync",
            "raw SQL",
            "drop",
            MigrationContractJustification.NeverReleased,
            "2.0.0",
            "OmnichannelContactPhoneIndex",
            "Drops the superseded phone index table that only ever existed inside the unreleased 2.0.0 line."),
        new MigrationContractEntry(
            "src/Modules/CrestApps.OrchardCore.Omnichannel.Managements/Migrations/OmnichannelContactsMigrations.cs",
            "OmnichannelContactsMigrations",
            "RemoveLegacyCollectionContactIndexTableAsync",
            "raw SQL",
            "drop",
            MigrationContractJustification.NeverReleased,
            "2.0.0",
            "OmnichannelContactIndex",
            "Drops the superseded collection-contact index table that only ever existed inside the unreleased 2.0.0 line, and refuses to drop while it still holds rows."),
    ];

    private static readonly ReviewedDynamicSqlEntry[] _reviewedDynamicSqlSites =
    [
        new ReviewedDynamicSqlEntry(
            "src/Modules/CrestApps.OrchardCore.ContactCenter/Migrations/ContactCenterMigrationSql.cs",
            "ContactCenterMigrationSql",
            "ExistsAsync",
            "0cba6ca11051069a",
            "Shared existence probe whose statement arrives as a parameter. Every caller in the scanned surface passes a literal SELECT, and the helper only reads a scalar, so it cannot alter schema regardless of the caller."),
        new ReviewedDynamicSqlEntry(
            "src/Modules/CrestApps.OrchardCore.ContactCenter/Migrations/ContactCenterMigrationSql.cs",
            "ContactCenterMigrationSql",
            "CreateUniqueIndexAsync",
            "0cba6ca11051069a",
            "Builds a CREATE UNIQUE INDEX statement from the dialect, table prefix, index name, and column names. The statement is additive by construction: it only ever creates an index and never drops or alters an existing object."),
    ];

    /// <summary>
    /// Fails when a migration performs a destructive schema change that the contract register does not authorize.
    /// </summary>
    [Fact]
    public void Migrations_WhenAStepIsDestructive_AreAuthorizedByTheContractRegister()
    {
        var repositoryRoot = FindRepositoryRoot();
        var occurrences = CollectOccurrences(repositoryRoot);

        Assert.NotEmpty(occurrences);

        var violations = occurrences
            .Where(occurrence => !occurrence.IsUndeterminable)
            .Where(occurrence => !_authorizedContractSteps.Any(entry => entry.Matches(occurrence)))
            .Select(occurrence => $"{occurrence.RelativePath}({occurrence.Line}): {occurrence.TypeName}.{occurrence.MethodName} performs '{occurrence.Operation}' on '{occurrence.Target}'.")
            .OrderBy(message => message, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            violations.Count == 0,
            "Data migrations must be additive so that an old node keeps working against the new schema during a rolling upgrade. " +
            "Each destructive step below is unauthorized. Either replace it with an additive step, defer the removal to a later " +
            "release once no supported version reads the object, or add a justified entry to the contract register in " +
            $"{nameof(MigrationAdditiveOnlyGuardTests)}.{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    /// <summary>
    /// Fails when a contract register entry does not authorize exactly one destructive step in the current code.
    /// </summary>
    [Fact]
    public void MigrationContractRegister_WhenAnEntryDoesNotAuthorizeExactlyOneStep_DoesNotExist()
    {
        var repositoryRoot = FindRepositoryRoot();
        var occurrences = CollectOccurrences(repositoryRoot);

        var violations = new List<string>();

        foreach (var entry in _authorizedContractSteps)
        {
            var matches = occurrences.Count(occurrence => entry.Matches(occurrence));

            if (matches == 1)
            {
                continue;
            }

            violations.Add(matches == 0
                ? $"{entry.RelativePath}: {entry.TypeName}.{entry.MethodName} no longer performs '{entry.Operation}' on '{entry.Target}'. Remove the stale register entry."
                : $"{entry.RelativePath}: {entry.TypeName}.{entry.MethodName} performs '{entry.Operation}' on '{entry.Target}' {matches} times. Split the register entry so each authorization covers exactly one step.");
        }

        violations.Sort(StringComparer.Ordinal);

        Assert.True(
            violations.Count == 0,
            "Each contract register entry authorizes one destructive step against one named object, so that an entry cannot " +
            "silently widen into a standing exemption for whatever the method does later." +
            $"{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    /// <summary>
    /// Fails when a contract register entry carries a justification the repository's own history does not support.
    /// </summary>
    [Fact]
    public void MigrationContractRegister_WhenAJustificationIsNotSupported_DoesNotExist()
    {
        var repositoryRoot = FindRepositoryRoot();
        var currentVersion = ReadCurrentVersion(repositoryRoot);
        var releasedVersions = _authorizedContractSteps.Any(entry => entry.Justification == MigrationContractJustification.NeverReleased)
            ? ReadStableReleaseVersions(repositoryRoot)
            : new List<Version>();

        var violations = _authorizedContractSteps
            .Select(entry => new
            {
                Entry = entry,
                Failure = DescribeJustificationFailure(
                    entry,
                    currentVersion,
                    releasedVersions,
                    databaseObject => ReadStableTagsDeclaring(repositoryRoot, databaseObject)),
            })
            .Where(candidate => candidate.Failure is not null)
            .Select(candidate => $"{candidate.Entry.RelativePath}: {candidate.Entry.TypeName}.{candidate.Entry.MethodName} '{candidate.Entry.Operation}' on '{candidate.Entry.Target}' — {candidate.Failure}")
            .OrderBy(message => message, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"Contract register justifications are checked against the repository's version and release history rather than trusted.{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    /// <summary>
    /// Fails when a never-released claim is not bound to the database object its register entry actually authorizes.
    /// </summary>
    [Fact]
    public void ContractRegister_WhenANeverReleasedClaimIsNotBoundToTheAuthorizedObject_DoesNotExist()
    {
        var occurrences = CollectOccurrences(FindRepositoryRoot());
        var violations = new List<string>();

        foreach (var entry in _authorizedContractSteps.Where(entry => entry.Justification == MigrationContractJustification.NeverReleased))
        {
            var location = $"{entry.RelativePath}: {entry.TypeName}.{entry.MethodName} authorizes '{entry.Operation}' on '{entry.Target}'";

            // A schema operation names its object directly. Raw SQL names only its verb, so the claim is bound to the
            // reconstructed statement instead: without this, changing the constant that names the dropped table would
            // leave the occurrence, the authorization, and the claim all unchanged while dropping something else.
            if (_destructiveSchemaOperations.Contains(entry.Operation))
            {
                if (!string.Equals(entry.NeverReleasedObject, entry.Target, StringComparison.Ordinal))
                {
                    violations.Add($"{location} but claims '{entry.NeverReleasedObject}' never shipped.");
                }

                continue;
            }

            var statement = occurrences.SingleOrDefault(occurrence => entry.Matches(occurrence))?.Statement;

            if (statement is null)
            {
                violations.Add($"{location} but its statement could not be read, so the claim cannot be bound to it.");

                continue;
            }

            var operands = ExtractDestructiveOperands(statement);
            var readable = statement.Replace(RuntimeHolePlaceholder, '?');

            if (operands.Count == 0)
            {
                violations.Add($"{location} but the object it operates on could not be read from the statement, so the claim cannot be bound to it: {readable}");

                continue;
            }

            // Every operand must be the claimed object. A statement that operates on a second object is not covered by a
            // single claim, so it is rejected rather than bound to whichever operand happens to appear first.
            var unclaimed = operands
                .Where(operand => operand.Length == 0 || !string.Equals(operand, entry.NeverReleasedObject, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (unclaimed.Count > 0)
            {
                var described = string.Join("', '", unclaimed.Select(operand => operand.Length == 0 ? "?" : operand));

                violations.Add($"{location} but claims '{entry.NeverReleasedObject}' never shipped, while the statement operates on '{described}': {readable}");
            }
        }

        violations.Sort(StringComparer.Ordinal);

        Assert.True(
            violations.Count == 0,
            "A never-released claim is checked by searching released source for the object it names, so a claim that is not " +
            "bound to the object the entry actually operates on would verify something the entry does not authorize." +
            $"{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    /// <summary>
    /// Fails when the repository declares its own <c>SqlBuilder</c>, which would make the read-only query-builder oracle spoofable.
    /// </summary>
    [Fact]
    public void QueryBuilderReconstruction_WhenTheRepositoryDeclaresItsOwnQueryBuilder_DoesNotExist()
    {
        var repositoryRoot = FindRepositoryRoot();
        var impersonations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (IsGeneratedPath(file))
            {
                continue;
            }

            var text = File.ReadAllText(file);

            if (!text.Contains(QueryBuilderTypeName, StringComparison.Ordinal))
            {
                continue;
            }

            var root = CSharpSyntaxTree
                .ParseText(text, cancellationToken: TestContext.Current.CancellationToken)
                .GetRoot(TestContext.Current.CancellationToken);
            var relativePath = Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/');

            impersonations.AddRange(root
                .DescendantNodes()
                .OfType<TypeDeclarationSyntax>()
                .Where(declaration => string.Equals(declaration.Identifier.ValueText, QueryBuilderTypeName, StringComparison.Ordinal))
                .Select(_ => $"{relativePath}: declares a type named '{QueryBuilderTypeName}'."));

            // An alias is the same impersonation without the declaration: 'using SqlBuilder = Something;' makes
            // 'new SqlBuilder(...)' construct a different type entirely, and a syntactic name check cannot see it.
            impersonations.AddRange(root
                .DescendantNodes()
                .OfType<UsingDirectiveSyntax>()
                .Where(directive => string.Equals(directive.Alias?.Name.Identifier.ValueText, QueryBuilderTypeName, StringComparison.Ordinal))
                .Select(directive => $"{relativePath}: aliases '{QueryBuilderTypeName}' to '{directive.Name}'."));
        }

        impersonations.Sort(StringComparer.Ordinal);

        Assert.True(
            impersonations.Count == 0,
            "The read-only query-builder oracle treats 'new SqlBuilder(...)' composed only with read-only operations as a SELECT, " +
            "which is only sound while the single 'SqlBuilder' in scope is YesSql's. Anything here could return destructive SQL " +
            $"from 'ToSqlString()' and pass. Rename it, or remove the oracle's special case.{Environment.NewLine}{string.Join(Environment.NewLine, impersonations)}");
    }

    /// <summary>
    /// Fails when a migration executes SQL this gate cannot read and no reviewer has recorded what the statement does.
    /// </summary>
    [Fact]
    public void Migrations_WhenSqlCannotBeRead_AreCoveredByTheReviewedDynamicSqlRegister()
    {
        var repositoryRoot = FindRepositoryRoot();
        var occurrences = CollectOccurrences(repositoryRoot);

        var violations = occurrences
            .Where(occurrence => occurrence.IsUndeterminable)
            .Where(occurrence => !_reviewedDynamicSqlSites.Any(entry => entry.Matches(occurrence)))
            .Select(occurrence => $"{occurrence.RelativePath}({occurrence.Line}): {occurrence.TypeName}.{occurrence.MethodName} executes SQL that cannot be read statically.")
            .OrderBy(message => message, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            violations.Count == 0,
            "A migration that builds its statement at runtime hides whether the change is additive, which is exactly what a " +
            "literal scan misses. Either compose the statement so the leading verb is visible, or record the call site in the " +
            $"reviewed dynamic SQL register with what the statement does and why it cannot be destructive.{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    /// <summary>
    /// Fails when a reviewed dynamic SQL entry does not cover exactly one unreadable statement in the current code.
    /// </summary>
    [Fact]
    public void ReviewedDynamicSqlRegister_WhenAnEntryDoesNotCoverExactlyOneStatement_DoesNotExist()
    {
        var repositoryRoot = FindRepositoryRoot();
        var occurrences = CollectOccurrences(repositoryRoot);

        var violations = new List<string>();

        foreach (var entry in _reviewedDynamicSqlSites)
        {
            var matches = occurrences.Count(occurrence => entry.Matches(occurrence));

            if (matches == 1)
            {
                continue;
            }

            var siteMatches = occurrences.Where(occurrence => entry.MatchesSite(occurrence)).ToList();

            if (matches == 0 && siteMatches.Count > 0)
            {
                violations.Add($"{entry.RelativePath}: {entry.TypeName} changed since '{entry.TypeName}.{entry.MethodName}' was approved. The approval covers a statement this gate cannot read, so it is pinned to the declaring type; re-review the statement and update the fingerprint to '{siteMatches[0].DeclaringTypeFingerprint}'.");

                continue;
            }

            violations.Add(matches == 0
                ? $"{entry.RelativePath}: {entry.TypeName}.{entry.MethodName} no longer executes unreadable SQL. Remove the stale register entry."
                : $"{entry.RelativePath}: {entry.TypeName}.{entry.MethodName} executes {matches} unreadable statements. Make them readable, or split the method so each reviewed statement stands alone.");
        }

        violations.Sort(StringComparer.Ordinal);

        Assert.True(
            violations.Count == 0,
            $"A reviewed dynamic SQL entry records a human decision about one specific statement, so it must not drift.{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    /// <summary>
    /// Fails when a reviewed dynamic SQL entry does not explain why the statement it covers cannot be destructive.
    /// </summary>
    [Fact]
    public void ReviewedDynamicSqlRegister_WhenAnEntryDoesNotExplainItself_DoesNotExist()
    {
        var violations = _reviewedDynamicSqlSites
            .Where(entry => string.IsNullOrWhiteSpace(entry.Rationale) || entry.Rationale.Length < 40)
            .Select(entry => $"{entry.RelativePath}: {entry.TypeName}.{entry.MethodName}")
            .OrderBy(message => message, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            violations.Count == 0,
            $"Every reviewed dynamic SQL entry must state what the statement does and why it cannot be destructive.{Environment.NewLine}{string.Join(Environment.NewLine, violations)}");
    }

    /// <summary>
    /// Fails when a registered data migration lives outside the folder convention this gate scans.
    /// </summary>
    [Fact]
    public void MigrationDiscovery_WhenAMigrationIsRegisteredOutsideTheScannedSurface_DoesNotExist()
    {
        var repositoryRoot = FindRepositoryRoot();
        var scannedTypeNames = EnumerateMigrationFiles(repositoryRoot)
            .SelectMany(file => CSharpSyntaxTree
                .ParseText(File.ReadAllText(file))
                .GetRoot()
                .DescendantNodes()
                .OfType<TypeDeclarationSyntax>()
                .Select(declaration => declaration.Identifier.ValueText))
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(scannedTypeNames);

        var registeredTypeNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (var file in Directory.EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.cs", SearchOption.AllDirectories))
        {
            if (IsGeneratedPath(file))
            {
                continue;
            }

            foreach (Match match in _dataMigrationRegistrationRegex.Matches(File.ReadAllText(file)))
            {
                registeredTypeNames.Add(match.Groups["type"].Value);
            }
        }

        Assert.NotEmpty(registeredTypeNames);

        var missing = registeredTypeNames
            .Where(typeName => !scannedTypeNames.Contains(typeName))
            .OrderBy(typeName => typeName, StringComparer.Ordinal)
            .ToList();

        Assert.True(
            missing.Count == 0,
            "This gate finds migrations by folder convention, which is only sound while every registered migration is declared " +
            "in a file under a 'Migrations' folder. The following registered migrations are outside that surface and are " +
            $"therefore unguarded. Move them, or widen the scan.{Environment.NewLine}{string.Join(Environment.NewLine, missing)}");
    }

    /// <summary>
    /// Verifies the oracles classify destructive and additive migration steps written in the shapes C# actually allows.
    /// </summary>
    /// <param name="statement">The migration statement under test.</param>
    /// <param name="expectedOperation">The operation the oracles are expected to report, or <c>null</c> when the statement is additive.</param>
    /// <param name="expectedTarget">The target the oracles are expected to report, or <c>null</c> when the statement is additive.</param>
    [Theory]
    [InlineData("table.AddColumn<string>(\"Name\");", null, null)]
    [InlineData("builder.CreateMapIndexTable<ThingIndex>(table => table.Column<string>(\"Name\"));", null, null)]
    [InlineData("table.DropColumn(\"Name\");", "DropColumn", "Name")]
    [InlineData("table.DropIndex(\"IDX_Thing_Name\");", "DropIndex", "IDX_Thing_Name")]
    [InlineData("table.RenameColumn(\"Old\", \"New\");", "RenameColumn", "Old")]
    [InlineData("table.RenameTable(\"Old\");", "RenameTable", "Old")]
    [InlineData("table.AlterColumn(\"Name\", column => column.WithLength(10));", "AlterColumn", "Name")]
    [InlineData("await connection.ExecuteAsync(\"drop table Things\");", "raw SQL", "drop")]
    [InlineData("await connection.ExecuteAsync(\"drop \" + \"table \" + quotedTable);", "raw SQL", "drop")]
    [InlineData("await connection.ExecuteAsync($\"drop table {quotedTable}\");", "raw SQL", "drop")]
    [InlineData("await connection.ExecuteAsync($\"truncate table {quotedTable}\");", "raw SQL", "truncate")]
    [InlineData("await connection.ExecuteAsync(\"-- cleanup\\ndelete from Things\");", "raw SQL", "delete")]
    [InlineData("await connection.ExecuteAsync(\"/* cleanup */ alter table Things drop column Name\");", "raw SQL", "alter")]
    [InlineData("await connection.ExecuteAsync(statement);", "raw SQL", "(undeterminable)")]
    [InlineData("await connection.ExecuteAsync($\"{verb} table {quotedTable}\");", "raw SQL", "(undeterminable)")]
    [InlineData("await connection.ExecuteAsync($\"select count(*) from {quotedTable}\");", null, null)]
    [InlineData("await connection.ExecuteAsync(\"update Things set Name = 'x'\");", null, null)]
    [InlineData("command.ExecuteNonQueryAsync();", null, null)]
    [InlineData("command.CommandText = \"drop table Things\";", "raw SQL", "drop")]
    [InlineData("command.CommandText = \"select 1\";", null, null)]
    [InlineData("command.CommandText = BuildStatement(name);", "raw SQL", "(undeterminable)")]
    [InlineData("var sql = $\"drop table {quotedTable}\"; await connection.ExecuteAsync(sql);", "raw SQL", "drop")]
    [InlineData("var sql = \"select 1\"; await connection.ExecuteAsync(sql);", null, null)]
    [InlineData("var sql = \"select 1\"; sql = BuildStatement(); await connection.ExecuteAsync(sql);", "raw SQL", "(undeterminable)")]
    [InlineData("var builder = new SqlBuilder(prefix, dialect); builder.AddSelector(id); builder.From(table); builder.WhereAnd(filter); await connection.QueryAsync<Document>(builder.ToSqlString());", null, null)]
    [InlineData("var builder = new SqlBuilder(prefix, dialect); builder.From(table); builder.Trail(extra); await connection.QueryAsync<Document>(builder.ToSqlString());", "raw SQL", "(undeterminable)")]
    [InlineData("connection.Execute(\"drop table Things\");", "raw SQL", "drop")]
    [InlineData("connection.Query<Document>(\"delete from Things returning *\");", "raw SQL", "delete")]
    [InlineData("await connection.ExecuteAsync(\"with doomed as (select Id from Things) delete from Things where Id in (select Id from doomed)\");", "raw SQL", "delete")]
    [InlineData("await connection.ExecuteAsync(\"with kept as (select Id from Things) select * from kept\");", null, null)]
    [InlineData("await connection.ExecuteAsync(\"select 1; drop table Things\");", "raw SQL", "drop")]
    [InlineData("await connection.ExecuteAsync(\"select Name from Things where Name = 'drop table'\");", null, null)]
    [InlineData("await connection.ExecuteAsync(\"exec('drop table Things')\");", "raw SQL", "(undeterminable)")]
    [InlineData("await connection.ExecuteAsync(\"sp_executesql N'drop table Things'\");", "raw SQL", "(undeterminable)")]
    [InlineData("await connection.ExecuteAsync(\"do $$ begin execute 'drop table Things'; end $$;\");", "raw SQL", "(undeterminable)")]
    [InlineData("await connection.ExecuteAsync(\"begin exec('drop table Things') end\");", "raw SQL", "(undeterminable)")]
    [InlineData("await connection.ExecuteAsync(\"select 1 from Things where Name = 'begin'\");", null, null)]
    public void Oracles_WhenAppliedToAStatement_ReportTheExpectedOccurrence(string statement, string expectedOperation, string expectedTarget)
    {
        var source = $$"""
            using System.Data.Common;
            using System.Threading.Tasks;
            using Dapper;
            using YesSql;
            using YesSql.Sql;

            namespace Probe.Migrations;

            public sealed class ProbeMigrations
            {
                private readonly DbConnection connection;

                private readonly string prefix;

                private readonly ISqlDialect dialect;

                private readonly string table;

                private readonly string id;

                private readonly string filter;

                private readonly string extra;

                public async Task<int> UpdateFrom1Async()
                {
                    {{statement}}

                    return 2;
                }
            }
            """;

        var occurrences = ExtractOccurrences(source, "src/Probe/Migrations/ProbeMigrations.cs");

        if (expectedOperation is null)
        {
            Assert.Empty(occurrences);

            return;
        }

        var occurrence = Assert.Single(occurrences);

        Assert.Equal("ProbeMigrations", occurrence.TypeName);
        Assert.Equal("UpdateFrom1Async", occurrence.MethodName);
        Assert.Equal(expectedOperation, occurrence.Operation);
        Assert.Equal(expectedTarget, occurrence.Target);
    }

    /// <summary>
    /// Verifies every object a destructive statement operates on is read from operand positions rather than from anywhere in the statement.
    /// </summary>
    /// <param name="statement">The reconstructed statement under test.</param>
    /// <param name="expectedOperands">The comma-separated objects the statement operates on, empty when none can be read.</param>
    [Theory]
    [InlineData("drop table Things", "Things")]
    [InlineData("drop table if exists Things", "Things")]
    [InlineData("drop table \"Things\"", "Things")]
    [InlineData("drop table [dbo].[Things]", "Things")]
    [InlineData("drop table dbo.Things;", "Things")]
    [InlineData("drop index IDX_Things_Name", "IDX_Things_Name")]
    [InlineData("truncate table Things", "Things")]
    [InlineData("alter table Things drop column Name", "Things")]
    [InlineData("delete from Things where Id = 1", "Things")]
    [InlineData("drop table Decoy /* Things */", "Decoy")]
    [InlineData("drop table Decoy -- Things", "Decoy")]
    [InlineData("drop table Decoy where Name = 'Things'", "Decoy")]
    [InlineData("drop table \u0001", "")]
    [InlineData("drop table Things, Others", "Things,Others")]
    [InlineData("drop table dbo.Things, dbo.Others", "Things,Others")]
    [InlineData("drop table Things; drop table Others", "Things,Others")]
    [InlineData("drop table Things; drop table Things", "Things")]
    [InlineData("select 1 from Things", "")]
    public void DestructiveOperand_WhenReadFromAStatement_IsEveryObjectTheStatementOperatesOn(string statement, string expectedOperands)
    {
        Assert.Equal(expectedOperands, string.Join(",", ExtractDestructiveOperands(statement)));
    }

    /// <summary>
    /// Fails when the data-layer calls reconstruction passes through no longer bind, which would otherwise surface as an unrelated register failure.
    /// </summary>
    [Fact]
    public void SemanticResolution_WhenMigrationsAreParsed_BindsTheDataLayerCallsReconstructionDependsOn()
    {
        var repositoryRoot = FindRepositoryRoot();
        var unresolved = new List<string>();
        var resolved = 0;

        foreach (var file in EnumerateMigrationFiles(repositoryRoot))
        {
            var relativePath = Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/');
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(file), cancellationToken: TestContext.Current.CancellationToken);

            var model = CSharpCompilation
                .Create("MigrationAdditiveOnlyGuard", [_implicitUsings.Value, tree], _metadataReferences.Value)
                .GetSemanticModel(tree);

            foreach (var node in tree.GetRoot(TestContext.Current.CancellationToken).DescendantNodes())
            {
                var expectation = node switch
                {
                    InvocationExpressionSyntax invocation when invocation.Expression is MemberAccessExpressionSyntax access
                        && _identityPreservingSqlOperations.TryGetValue(access.Name.Identifier.ValueText, out var declaring)
                        => (Description: access.Name.Identifier.ValueText, Expected: declaring),
                    ObjectCreationExpressionSyntax creation when string.Equals(creation.Type.ToString(), QueryBuilderTypeName, StringComparison.Ordinal)
                        => (Description: QueryBuilderTypeName, Expected: QueryBuilderTypeFullName),
                    _ => default((string Description, string Expected)?),
                };

                if (expectation is null)
                {
                    continue;
                }

                var containingType = (model.GetSymbolInfo(node, TestContext.Current.CancellationToken).Symbol as IMethodSymbol)?.ContainingType?.ToDisplayString();

                if (string.Equals(containingType, expectation.Value.Expected, StringComparison.Ordinal))
                {
                    resolved++;

                    continue;
                }

                unresolved.Add($"{relativePath}: '{expectation.Value.Description}' resolved to '{containingType ?? "nothing"}' instead of '{expectation.Value.Expected}'.");
            }
        }

        unresolved.Sort(StringComparer.Ordinal);

        Assert.True(
            unresolved.Count == 0,
            "Reconstruction passes through a call only when the resolved symbol belongs to the data layer's own type. If these stop " +
            "resolving because the compilation lost a reference, every statement becomes unreadable and the registers fail for a " +
            "reason that has nothing to do with what the migrations do." +
            $"{Environment.NewLine}{string.Join(Environment.NewLine, unresolved)}");

        // A silently empty scan would make the check vacuous, so the calls it is meant to prove must actually be present.
        Assert.True(resolved > 0, "No data-layer calls were found to resolve, so this check proved nothing.");
    }

    /// <summary>
    /// Verifies the object a migration drops is reconstructed through the constants, quoting, and naming conventions real migrations use.
    /// </summary>
    [Fact]
    public void Oracles_WhenTheDroppedObjectIsNamedIndirectly_ReconstructIt()
    {
        var source = """
            using System.Data.Common;
            using System.Threading.Tasks;
            using Dapper;
            using YesSql;

            namespace Probe.Migrations;

            public sealed class ProbeMigrations
            {
                private const string LegacyTableName = "LegacyThings";

                private readonly IStore _store;

                private readonly DbConnection connection;

                public async Task<int> UpdateFrom1Async()
                {
                    var dialect = _store.Configuration.SqlDialect;
                    var table = $"{_store.Configuration.TablePrefix}{LegacyTableName}";
                    var quotedTable = dialect.QuoteForTableName(table, _store.Configuration.Schema);

                    await connection.ExecuteAsync($"drop table {quotedTable}");

                    return 2;
                }

                public async Task<int> UpdateFrom2Async()
                {
                    var dialect = _store.Configuration.SqlDialect;
                    var tableName = _store.Configuration.TableNameConvention.GetIndexTable(typeof(ThingIndex), "shared");
                    var quotedTable = dialect.QuoteForTableName($"{_store.Configuration.TablePrefix}{tableName}", _store.Configuration.Schema);

                    await connection.ExecuteAsync($"drop table {quotedTable}");

                    return 3;
                }
            }
            """;

        var occurrences = ExtractOccurrences(source, "src/Probe/Migrations/ProbeMigrations.cs");

        Assert.Equal(2, occurrences.Count);
        Assert.All(occurrences, occurrence => Assert.Equal("drop", occurrence.Target));

        Assert.Contains("LegacyThings", occurrences.Single(occurrence => occurrence.MethodName == "UpdateFrom1Async").Statement, StringComparison.Ordinal);
        Assert.Contains("ThingIndex", occurrences.Single(occurrence => occurrence.MethodName == "UpdateFrom2Async").Statement, StringComparison.Ordinal);
    }

    /// <summary>
    /// Verifies a look-alike quoting method on a type other than the data layer's dialect is not trusted to preserve the object's identity.
    /// </summary>
    [Fact]
    public void Oracles_WhenAnIdentityPreservingCallIsImpersonated_DoNotReconstructThroughIt()
    {
        var source = """
            using System.Data.Common;
            using System.Threading.Tasks;
            using Dapper;
            using YesSql;

            namespace Probe.Migrations;

            public sealed class ProbeMigrations
            {
                private const string LegacyTableName = "LegacyThings";

                private readonly IStore _store;

                private readonly DbConnection connection;

                public async Task<int> UpdateFrom1Async()
                {
                    var dialect = new MisleadingDialect();
                    var table = $"{_store.Configuration.TablePrefix}{LegacyTableName}";
                    var quotedTable = dialect.QuoteForTableName(table, _store.Configuration.Schema);

                    await connection.ExecuteAsync($"drop table {quotedTable}");

                    return 2;
                }

                private sealed class MisleadingDialect
                {
                    public string QuoteForTableName(string table, string schema) => "SomethingElse";
                }
            }
            """;

        var occurrence = Assert.Single(ExtractOccurrences(source, "src/Probe/Migrations/ProbeMigrations.cs"));

        Assert.Equal("drop", occurrence.Target);

        // The impersonating call is refused, so the object is unreadable rather than bound to the argument it was handed.
        Assert.DoesNotContain("LegacyThings", occurrence.Statement, StringComparison.Ordinal);
        Assert.DoesNotContain(ExtractDestructiveOperands(occurrence.Statement), operand => operand.Length > 0);
    }

    /// <summary>
    /// Verifies a destructive step is attributed to the private helper that performs it rather than to the step that calls it.
    /// </summary>
    [Fact]
    public void Oracles_WhenADestructiveStepIsInAPrivateHelper_AttributeItToTheHelper()
    {
        var source = """
            namespace Probe.Migrations;

            public sealed class ProbeMigrations
            {
                public async Task<int> UpdateFrom1Async()
                {
                    await CleanupAsync();

                    return 2;
                }

                private async Task CleanupAsync()
                {
                    await _builder.AlterTableAsync("Thing", table => table.DropColumn("Name"));
                }
            }
            """;

        var occurrence = Assert.Single(ExtractOccurrences(source, "src/Probe/Migrations/ProbeMigrations.cs"));

        Assert.Equal("CleanupAsync", occurrence.MethodName);
    }

    /// <summary>
    /// Verifies destructive steps inside <c>UninstallAsync</c> are exempt, because uninstall is not an upgrade path.
    /// </summary>
    [Fact]
    public void Oracles_WhenADestructiveStepIsInUninstall_ReportNoOccurrence()
    {
        var source = """
            namespace Probe.Migrations;

            public sealed class ProbeMigrations
            {
                public async Task<int> UninstallAsync()
                {
                    await _builder.DropMapIndexTableAsync<ThingIndex>();
                    await connection.ExecuteAsync($"drop table {quotedTable}");

                    return 1;
                }
            }
            """;

        Assert.Empty(ExtractOccurrences(source, "src/Probe/Migrations/ProbeMigrations.cs"));
    }

    /// <summary>
    /// Verifies a contract-phase justification is only accepted when the expand landed in a strictly older release.
    /// </summary>
    /// <param name="introducedInVersion">The version the register entry names as the release that introduced the object.</param>
    /// <param name="currentVersion">The version currently under development.</param>
    /// <param name="expectedToBeAccepted">Whether the justification is expected to be accepted.</param>
    [Theory]
    [InlineData("1.9.0", "2.0.0", true)]
    [InlineData("1.99.99", "2.0.0", true)]
    [InlineData("2.0.0", "2.0.0", false)]
    [InlineData("2.1.0", "2.0.0", false)]
    [InlineData("not-a-version", "2.0.0", false)]
    public void ContractPhaseJustification_WhenTheExpandDidNotLandEarlier_IsRejected(string introducedInVersion, string currentVersion, bool expectedToBeAccepted)
    {
        var entry = CreateProbeEntry(MigrationContractJustification.ContractPhase, introducedInVersion);

        var failure = DescribeJustificationFailure(entry, Version.Parse(currentVersion), [new Version(1, 2, 2)], _ => []);

        Assert.Equal(expectedToBeAccepted, failure is null);
    }

    /// <summary>
    /// Verifies a never-released justification is checked against the repository's stable release tags and fails closed without them.
    /// </summary>
    /// <param name="introducedInVersion">The version the register entry names as the release that introduced the object.</param>
    /// <param name="releasedVersions">The stable releases the repository has tagged, separated by semicolons.</param>
    /// <param name="neverReleasedObject">The database object the register entry claims never reached a customer.</param>
    /// <param name="declaringTags">The stable release tags whose source contains the object, separated by semicolons, or <c>"(unreadable)"</c> when the released source cannot be searched.</param>
    /// <param name="expectedToBeAccepted">Whether the justification is expected to be accepted.</param>
    [Theory]
    [InlineData("2.0.0", "1.2.2;1.2.1;1.0.0", "Name", "", true)]
    [InlineData("2.0.0", "1.2.2;2.0.0", "Name", "", false)]
    [InlineData("2.0.0", "1.2.2;2.1.0", "Name", "", false)]
    [InlineData("2.0.0", "", "Name", "", false)]
    [InlineData("not-a-version", "1.2.2", "Name", "", false)]
    [InlineData("2.0.0", "1.2.2", "", "", false)]
    [InlineData("2.0.0", "1.2.2", "   ", "", false)]
    [InlineData("2.0.0", "1.2.2", "Name", "(unreadable)", false)]
    [InlineData("2.0.0", "1.2.2", "Name", "v1.2.2", false)]
    [InlineData("2.0.0", "1.2.2", "Name", "v1.0.0;v1.2.2", false)]
    public void NeverReleasedJustification_WhenTheObjectIsPresentInAReleasedTree_IsRejected(
        string introducedInVersion,
        string releasedVersions,
        string neverReleasedObject,
        string declaringTags,
        bool expectedToBeAccepted)
    {
        var entry = CreateProbeEntry(MigrationContractJustification.NeverReleased, introducedInVersion, neverReleasedObject);

        var released = releasedVersions
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(Version.Parse)
            .ToList();

        var failure = DescribeJustificationFailure(
            entry,
            new Version(2, 0, 0),
            released,
            _ => declaringTags == "(unreadable)"
                ? null
                : [.. declaringTags.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)]);

        Assert.Equal(expectedToBeAccepted, failure is null);
    }

    /// <summary>
    /// Verifies the repository's own release history still supports every never-released justification in the register.
    /// </summary>
    [Fact]
    public void StableReleaseHistory_WhenReadFromTheRepository_IsAvailableAndBoundedByTheCurrentVersion()
    {
        var repositoryRoot = FindRepositoryRoot();
        var releasedVersions = ReadStableReleaseVersions(repositoryRoot);

        Assert.True(
            releasedVersions.Count > 0,
            "The never-released justification is checked against stable release tags, so the gate needs them. Run 'git fetch --tags', " +
            "and make sure CI checks out with 'fetch-depth: 0'.");

        Assert.True(
            releasedVersions.Max() < ReadCurrentVersion(repositoryRoot),
            "VersionPrefix must stay ahead of the newest stable release tag, otherwise the version currently under development has already shipped.");
    }

    private static MigrationContractEntry CreateProbeEntry(
        MigrationContractJustification justification,
        string introducedInVersion,
        string neverReleasedObject = "Name")
    {
        return new MigrationContractEntry(
            "src/Probe/Migrations/ProbeMigrations.cs",
            "ProbeMigrations",
            "UpdateFrom1Async",
            "DropColumn",
            "Name",
            justification,
            introducedInVersion,
            neverReleasedObject,
            "Probe entry used to exercise the justification rules directly, because the register may not contain every justification kind.");
    }

    private static string DescribeJustificationFailure(
        MigrationContractEntry entry,
        Version currentVersion,
        List<Version> releasedVersions,
        Func<string, List<string>> resolveStableTagsDeclaring)
    {
        if (string.IsNullOrWhiteSpace(entry.Rationale) || entry.Rationale.Length < 40)
        {
            return "the rationale must explain why the removal is safe for a customer upgrading from a supported release.";
        }

        if (!Version.TryParse(entry.IntroducedInVersion, out var introducedInVersion))
        {
            return $"'{entry.IntroducedInVersion}' is not a version. Name the release that introduced the object being removed.";
        }

        switch (entry.Justification)
        {
            case MigrationContractJustification.ContractPhase:
                if (introducedInVersion >= currentVersion)
                {
                    return $"the object was introduced in {introducedInVersion} and is being removed in {currentVersion}. Expand and contract must not land in the same release, because a customer upgrading in one step never runs a version that both writes and stops reading the object.";
                }

                return null;

            case MigrationContractJustification.NeverReleased:
                if (releasedVersions.Count == 0)
                {
                    return "the claim that no stable release shipped this object cannot be checked, because no stable release tags were found. Run 'git fetch --tags', and make sure CI checks out with 'fetch-depth: 0'.";
                }

                var shipped = releasedVersions.Where(version => version >= introducedInVersion).ToList();

                if (shipped.Count > 0)
                {
                    return $"the object was introduced in {introducedInVersion}, but {string.Join(", ", shipped.OrderBy(version => version))} shipped as a stable release. Treat this as a contract-phase removal instead.";
                }

                // The version alone is an author's assertion: an object that really shipped in 1.2.2 can be declared as
                // introduced in 2.0.0 and the version check would still clear it, because no stable 2.0.0 tag exists.
                // The claim is therefore checked against the shipped source itself rather than against anything the
                // author supplies: a database object that never reached a customer cannot appear in a released tree.
                if (string.IsNullOrWhiteSpace(entry.NeverReleasedObject))
                {
                    return "a never-released claim must name the database object it is about, so the claim can be checked against the released source.";
                }

                var declaringTags = resolveStableTagsDeclaring(entry.NeverReleasedObject);

                if (declaringTags is null)
                {
                    return $"the released source could not be searched for '{entry.NeverReleasedObject}', so the never-released claim cannot be verified.";
                }

                if (declaringTags.Count > 0)
                {
                    return $"'{entry.NeverReleasedObject}' is present in the source of stable release {string.Join(", ", declaringTags)}, so the object did ship. Treat this as a contract-phase removal instead.";
                }

                return null;

            case MigrationContractJustification.ReviewedAdditive:
                return null;

            default:
                return $"'{entry.Justification}' is not a recognized justification.";
        }
    }

    private static List<MigrationOccurrence> CollectOccurrences(string repositoryRoot)
    {
        var occurrences = new List<MigrationOccurrence>();

        foreach (var file in EnumerateMigrationFiles(repositoryRoot))
        {
            var relativePath = Path.GetRelativePath(repositoryRoot, file).Replace('\\', '/');

            occurrences.AddRange(ExtractOccurrences(File.ReadAllText(file), relativePath));
        }

        return occurrences;
    }

    private static IEnumerable<string> EnumerateMigrationFiles(string repositoryRoot)
    {
        return Directory
            .EnumerateFiles(Path.Combine(repositoryRoot, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(file => !IsGeneratedPath(file))
            .Where(file => file.Replace('\\', '/').Contains("/Migrations/", StringComparison.Ordinal))
            .OrderBy(file => file, StringComparer.Ordinal);
    }

    private static bool IsGeneratedPath(string path)
    {
        var normalized = path.Replace('\\', '/');

        return normalized.Contains("/obj/", StringComparison.Ordinal) || normalized.Contains("/bin/", StringComparison.Ordinal);
    }

    private static List<MigrationOccurrence> ExtractOccurrences(string source, string relativePath)
    {
        var tree = CSharpSyntaxTree.ParseText(source, cancellationToken: TestContext.Current.CancellationToken);
        var root = tree.GetRoot(TestContext.Current.CancellationToken);

        // A semantic model is what lets reconstruction check which type declares a call it passes through, instead of
        // trusting the member's name. References come from the test output, so every assembly a migration compiles
        // against is present; a call that still fails to bind is refused by the caller rather than assumed safe.
        var model = CSharpCompilation
            .Create("MigrationAdditiveOnlyGuard", [_implicitUsings.Value, tree], _metadataReferences.Value)
            .GetSemanticModel(tree);
        var occurrences = new List<MigrationOccurrence>();

        foreach (var node in root.DescendantNodes())
        {
            var occurrence = node switch
            {
                InvocationExpressionSyntax invocation => DescribeInvocation(invocation, new ReconstructionScope(FindEnclosingScope(node), model)),
                AssignmentExpressionSyntax assignment => DescribeCommandTextAssignment(assignment, new ReconstructionScope(FindEnclosingScope(node), model)),
                _ => default((string Operation, string Target, string Statement)?),
            };

            if (occurrence is null || IsExempt(node))
            {
                continue;
            }

            occurrences.Add(new MigrationOccurrence(
                relativePath,
                FindEnclosingTypeName(node),
                FindEnclosingMethodName(node),
                occurrence.Value.Operation,
                occurrence.Value.Target,
                occurrence.Value.Statement,
                node.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                ComputeDeclaringTypeFingerprint(node)));
        }

        return occurrences;
    }

    private static (string Operation, string Target, string Statement)? DescribeInvocation(InvocationExpressionSyntax invocation, ReconstructionScope scope)
    {
        var name = invocation.Expression switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            GenericNameSyntax generic => generic.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null,
        };

        if (name is null)
        {
            return null;
        }

        if (_destructiveSchemaOperations.Contains(name))
        {
            return (name, ReadFirstStringArgument(invocation) ?? string.Empty, null);
        }

        if (!_sqlExecutionOperations.Contains(name) || invocation.ArgumentList.Arguments.Count == 0)
        {
            return null;
        }

        var statement = invocation.ArgumentList.Arguments
            .Select(argument => ReconstructText(argument.Expression, scope, 0))
            .FirstOrDefault(text => text is not null);

        return statement is null
            ? null
            : ClassifySql(statement);
    }

    private static (string Operation, string Target, string Statement)? DescribeCommandTextAssignment(AssignmentExpressionSyntax assignment, ReconstructionScope scope)
    {
        if (!assignment.IsKind(SyntaxKind.SimpleAssignmentExpression))
        {
            return null;
        }

        var assignedMember = assignment.Left switch
        {
            MemberAccessExpressionSyntax memberAccess => memberAccess.Name.Identifier.ValueText,
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            _ => null,
        };

        if (!string.Equals(assignedMember, CommandTextMemberName, StringComparison.Ordinal))
        {
            return null;
        }

        var statement = ReconstructText(assignment.Right, scope, 0);

        return statement is null
            ? null
            : ClassifySql(statement);
    }

    private static (string Operation, string Target, string Statement)? ClassifySql(string statement)
    {
        var verb = ReadLeadingVerb(statement);

        if (verb is null)
        {
            return ("raw SQL", UndeterminableTarget, statement);
        }

        // A statement that runs another statement is unreadable by definition: the gate can see 'exec', not what it runs.
        if (_dynamicSqlExecutionVerbs.Contains(verb))
        {
            return ("raw SQL", UndeterminableTarget, statement);
        }

        if (_destructiveSqlVerbs.Contains(verb))
        {
            return ("raw SQL", verb.ToLowerInvariant(), statement);
        }

        // A destructive statement does not have to lead. A common table expression leads with 'with', and a batch can
        // separate statements with a semicolon, so the leading verb alone would clear 'with x as (...) delete from t'.
        // String literals are removed first so a value that merely reads like a verb is not mistaken for one; a literal
        // cannot execute on its own, and the statements that would execute one lead with a dynamic execution verb.
        var readable = StripSqlLiterals(StripSqlComments(statement));

        // Stripping literals is what makes the scan below precise, but a procedural block can execute a statement that
        // lives inside one, so a dynamic execution verb anywhere is unreadable rather than safe.
        if (_dynamicSqlExecutionVerbs.Any(candidate => Regex.IsMatch(readable, $@"\b{candidate}\b", RegexOptions.IgnoreCase)))
        {
            return ("raw SQL", UndeterminableTarget, statement);
        }

        var embedded = _destructiveSqlVerbs
            .Where(candidate => Regex.IsMatch(readable, $@"\b{candidate}\b", RegexOptions.IgnoreCase))
            .OrderBy(candidate => readable.IndexOf(candidate, StringComparison.OrdinalIgnoreCase))
            .FirstOrDefault();

        return embedded is null
            ? null
            : ("raw SQL", embedded, statement);
    }

    private static List<string> ExtractDestructiveOperands(string statement)
    {
        // The claim must be pinned to the identifier the statement actually operates on. Matching the object anywhere in
        // the statement would accept it appearing in a trailing comment while a different table is dropped, so comments
        // and quoted values are removed and only operand positions are read. Every destructive operand in the statement is
        // returned, because a batch can name one authorized object and then operate on a second, unauthorized one.
        var readable = StripSqlLiterals(StripSqlComments(statement));
        var operands = new List<string>();

        foreach (Match match in _destructiveOperandRegex.Matches(readable))
        {
            foreach (var named in match.Groups["object"].Value.Split(','))
            {
                // Quoting characters are removed wherever they appear, not only at the ends, so a schema-qualified and
                // quoted name such as [dbo].[Things] still reduces to the object it names.
                var operand = _identifierQuotingRegex.Replace(
                    named.Replace(RuntimeHolePlaceholder.ToString(), string.Empty),
                    string.Empty).Trim();

                // A schema-qualified name still identifies one object, and the qualifier is not what the claim is about.
                var separator = operand.LastIndexOf('.');

                if (separator >= 0)
                {
                    operand = operand.Substring(separator + 1);
                }

                // An operand that reduces to nothing is unreadable rather than absent, so it is kept and reported,
                // which makes the caller fail closed instead of treating the statement as having no operand.
                operands.Add(operand);
            }
        }

        return operands
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string StripSqlLiterals(string statement)
    {
        var builder = new StringBuilder(statement.Length);
        var insideLiteral = false;

        for (var index = 0; index < statement.Length; index++)
        {
            var character = statement[index];

            if (character == '\'')
            {
                insideLiteral = !insideLiteral;

                continue;
            }

            if (!insideLiteral)
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }

    private static string ReadFirstStringArgument(InvocationExpressionSyntax invocation)
    {
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            if (argument.Expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
            {
                return literal.Token.ValueText;
            }
        }

        return null;
    }

    private static string ReconstructText(ExpressionSyntax expression, ReconstructionScope scope, int depth)
    {
        switch (expression)
        {
            case LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression):
                return literal.Token.ValueText;

            case ParenthesizedExpressionSyntax parenthesized:
                return ReconstructText(parenthesized.Expression, scope, depth);

            case InterpolatedStringExpressionSyntax interpolated:
                var builder = new StringBuilder();

                foreach (var content in interpolated.Contents)
                {
                    if (content is InterpolatedStringTextSyntax text)
                    {
                        builder.Append(text.TextToken.ValueText);

                        continue;
                    }

                    builder.Append(content is InterpolationSyntax interpolation
                        ? ReconstructText(interpolation.Expression, scope, depth) ?? RuntimeHolePlaceholder.ToString()
                        : RuntimeHolePlaceholder.ToString());
                }

                return builder.ToString();

            case BinaryExpressionSyntax binary when binary.IsKind(SyntaxKind.AddExpression):
                var left = ReconstructText(binary.Left, scope, depth);
                var right = ReconstructText(binary.Right, scope, depth);

                if (left is null && right is null)
                {
                    return null;
                }

                return (left ?? RuntimeHolePlaceholder.ToString()) + (right ?? RuntimeHolePlaceholder.ToString());

            case IdentifierNameSyntax identifier:
                return ResolveLocal(identifier, scope, depth) ?? RuntimeHolePlaceholder.ToString();

            case InvocationExpressionSyntax invocation:
                return ResolveReadOnlyQueryBuilder(invocation, scope)
                    ?? ResolveIdentityPreservingCall(invocation, scope, depth)
                    ?? RuntimeHolePlaceholder.ToString();

            case TypeOfExpressionSyntax typeOf:
                return typeOf.Type is IdentifierNameSyntax typeName
                    ? typeName.Identifier.ValueText
                    : RuntimeHolePlaceholder.ToString();

            case MemberAccessExpressionSyntax:
            case ElementAccessExpressionSyntax:
            case ConditionalExpressionSyntax:
            case CastExpressionSyntax:
                return RuntimeHolePlaceholder.ToString();

            default:
                return null;
        }
    }

    private static string ResolveLocal(IdentifierNameSyntax identifier, ReconstructionScope scope, int depth)
    {
        if (scope?.Node is null || depth >= MaximumLocalResolutionDepth)
        {
            return null;
        }

        var name = identifier.Identifier.ValueText;

        var declarators = scope.Node
            .DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .Where(declarator => string.Equals(declarator.Identifier.ValueText, name, StringComparison.Ordinal))
            .ToList();

        var reassigned = scope.Node
            .DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Any(assignment => assignment.Left is IdentifierNameSyntax left
                && string.Equals(left.Identifier.ValueText, name, StringComparison.Ordinal));

        if (declarators.Count == 0)
        {
            return ResolveConstantField(identifier, scope, depth);
        }

        if (declarators.Count != 1 || reassigned || declarators[0].Initializer is null)
        {
            return null;
        }

        return ReconstructText(declarators[0].Initializer.Value, scope, depth + 1);
    }

    private static string ResolveConstantField(IdentifierNameSyntax identifier, ReconstructionScope scope, int depth)
    {
        var declaringType = scope.Node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();

        if (declaringType is null)
        {
            return null;
        }

        var name = identifier.Identifier.ValueText;

        // A constant is as statically known as a literal, and the object a migration drops is routinely named by one,
        // so refusing to resolve it would leave the dropped object unreadable and unbindable to its authorization.
        var constants = declaringType
            .Members
            .OfType<FieldDeclarationSyntax>()
            .Where(field => field.Modifiers.Any(SyntaxKind.ConstKeyword))
            .SelectMany(field => field.Declaration.Variables)
            .Where(variable => string.Equals(variable.Identifier.ValueText, name, StringComparison.Ordinal))
            .ToList();

        return constants.Count == 1 && constants[0].Initializer is not null
            ? ReconstructText(constants[0].Initializer.Value, scope with { Node = declaringType }, depth + 1)
            : null;
    }

    private static string ResolveIdentityPreservingCall(InvocationExpressionSyntax invocation, ReconstructionScope scope, int depth)
    {
        if (scope?.Model is null
            || invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || !_identityPreservingSqlOperations.TryGetValue(memberAccess.Name.Identifier.ValueText, out var declaringType)
            || invocation.ArgumentList.Arguments.Count == 0)
        {
            return null;
        }

        // The method name alone is not evidence. Any type can declare a member called QuoteForTableName and return a
        // different object, so the invocation is resolved to its symbol and the declaring type is checked. A call that
        // cannot be resolved is refused rather than trusted, which leaves the object unreadable and fails the register.
        if (scope.Model.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method
            || method.ContainingType is null
            || !string.Equals(method.ContainingType.ToDisplayString(), declaringType, StringComparison.Ordinal))
        {
            return null;
        }

        // These operations decorate or derive a name without changing which object is named: quoting adds delimiters, and
        // a table-name convention maps an index type to its table. Reconstructing through them is what lets the object a
        // statement drops be bound to the authorization that permits dropping it.
        return ReconstructText(invocation.ArgumentList.Arguments[0].Expression, scope, depth + 1);
    }

    private static string ResolveReadOnlyQueryBuilder(InvocationExpressionSyntax invocation, ReconstructionScope scope)
    {
        if (scope?.Node is null
            || invocation.Expression is not MemberAccessExpressionSyntax memberAccess
            || !string.Equals(memberAccess.Name.Identifier.ValueText, "ToSqlString", StringComparison.Ordinal)
            || memberAccess.Expression is not IdentifierNameSyntax builderIdentifier)
        {
            return null;
        }

        var builderName = builderIdentifier.Identifier.ValueText;

        var declarators = scope.Node
            .DescendantNodes()
            .OfType<VariableDeclaratorSyntax>()
            .Where(declarator => string.Equals(declarator.Identifier.ValueText, builderName, StringComparison.Ordinal))
            .ToList();

        if (declarators.Count != 1
            || declarators[0].Initializer?.Value is not ObjectCreationExpressionSyntax creation)
        {
            return null;
        }

        // Treating a builder chain as a SELECT is only sound for the data layer's own builder. The type name is checked
        // through the resolved symbol so a local type spelled the same way is refused rather than trusted.
        if (scope.Model.GetSymbolInfo(creation).Symbol is not IMethodSymbol constructor
            || !string.Equals(constructor.ContainingType?.ToDisplayString(), QueryBuilderTypeFullName, StringComparison.Ordinal))
        {
            return null;
        }

        var reassigned = scope.Node
            .DescendantNodes()
            .OfType<AssignmentExpressionSyntax>()
            .Any(assignment => assignment.Left is IdentifierNameSyntax left
                && string.Equals(left.Identifier.ValueText, builderName, StringComparison.Ordinal));

        if (reassigned)
        {
            return null;
        }

        var composedWith = scope.Node
            .DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .Where(candidate => candidate.Expression is MemberAccessExpressionSyntax candidateAccess
                && candidateAccess.Expression is IdentifierNameSyntax candidateIdentifier
                && string.Equals(candidateIdentifier.Identifier.ValueText, builderName, StringComparison.Ordinal))
            .Select(candidate => ((MemberAccessExpressionSyntax)candidate.Expression).Name.Identifier.ValueText);

        return composedWith.All(_readOnlySqlBuilderOperations.Contains)
            ? "select "
            : null;
    }

    private static SyntaxNode FindEnclosingScope(SyntaxNode node)
    {
        return node.Ancestors().FirstOrDefault(ancestor => ancestor is MethodDeclarationSyntax
            or LocalFunctionStatementSyntax
            or ConstructorDeclarationSyntax);
    }

    private static string ReadLeadingVerb(string statement)
    {
        var normalized = StripSqlComments(statement).TrimStart(' ', '\t', '\r', '\n', '(', ';');

        if (normalized.Length == 0 || !char.IsLetter(normalized[0]))
        {
            return null;
        }

        var length = 0;

        while (length < normalized.Length && (char.IsLetter(normalized[length]) || normalized[length] == '_'))
        {
            length++;
        }

        return normalized.Substring(0, length);
    }

    private static string StripSqlComments(string statement)
    {
        var withoutBlockComments = Regex.Replace(statement, @"/\*.*?\*/", " ", RegexOptions.Singleline);

        return Regex.Replace(withoutBlockComments, @"--[^\r\n]*", " ");
    }

    private static bool IsExempt(SyntaxNode node)
    {
        return string.Equals(FindEnclosingMethodName(node), UninstallMethodName, StringComparison.Ordinal);
    }

    private static string FindEnclosingMethodName(SyntaxNode node)
    {
        foreach (var ancestor in node.Ancestors())
        {
            switch (ancestor)
            {
                case MethodDeclarationSyntax method:
                    return method.Identifier.ValueText;

                case LocalFunctionStatementSyntax localFunction:
                    return localFunction.Identifier.ValueText;

                case ConstructorDeclarationSyntax constructor:
                    return constructor.Identifier.ValueText;
            }
        }

        return "(file scope)";
    }

    private static string ComputeDeclaringTypeFingerprint(SyntaxNode node)
    {
        var declaration = node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault();

        if (declaration is null)
        {
            return string.Empty;
        }

        var normalized = Regex.Replace(declaration.ToFullString(), @"\s+", " ").Trim();
        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));

        return Convert.ToHexString(digest, 0, 8).ToLowerInvariant();
    }

    private static string FindEnclosingTypeName(SyntaxNode node)
    {
        return node.Ancestors().OfType<TypeDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText ?? "(unknown)";
    }

    private static Version ReadCurrentVersion(string repositoryRoot)
    {
        var document = XDocument.Load(Path.Combine(repositoryRoot, "Directory.Build.props"));

        var versionPrefix = document
            .Descendants("VersionPrefix")
            .Select(element => element.Value.Trim())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        Assert.False(string.IsNullOrWhiteSpace(versionPrefix), "Directory.Build.props must declare a VersionPrefix.");

        return Version.Parse(versionPrefix);
    }

    private static List<string> ReadStableTagsDeclaring(string repositoryRoot, string databaseObject)
    {
        var tags = RunGit(repositoryRoot, "tag --list")
            ?.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(tag => _stableReleaseTagRegex.IsMatch(tag))
            .OrderBy(tag => tag, StringComparer.Ordinal)
            .ToList();

        if (tags is null || tags.Count == 0)
        {
            return null;
        }

        var declaring = new List<string>();

        foreach (var tag in tags)
        {
            // 'git grep' exits 1 when it matches nothing, which RunGit reports as null, so the two outcomes are
            // distinguished by first proving the tree can be read at all.
            if (RunGit(repositoryRoot, $"grep --name-only -w -F -e ReadableProbe {tag} -- src/") is null
                && RunGit(repositoryRoot, $"rev-parse {tag}^{{tree}}") is null)
            {
                return null;
            }

            if (RunGit(repositoryRoot, $"grep --name-only -w -F -e {databaseObject} {tag} -- src/") is not null)
            {
                declaring.Add(tag);
            }
        }

        return declaring;
    }

    private static string RunGit(string repositoryRoot, string arguments)
    {
        var startInfo = new ProcessStartInfo("git", arguments)
        {
            WorkingDirectory = repositoryRoot,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        using var process = Process.Start(startInfo);

        Assert.NotNull(process);

        var output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        return process.ExitCode == 0
            ? output
            : null;
    }

    private static List<Version> ReadStableReleaseVersions(string repositoryRoot)
    {
        var output = RunGit(repositoryRoot, "tag --list");

        if (output is null)
        {
            return [];
        }

        return output
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(tag => _stableReleaseTagRegex.Match(tag))
            .Where(match => match.Success)
            .Select(match => Version.Parse(match.Groups["version"].Value))
            .Distinct()
            .ToList();
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "CrestApps.OrchardCore.slnx")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);

        return directory.FullName;
    }

    private sealed record MigrationOccurrence(
        string RelativePath,
        string TypeName,
        string MethodName,
        string Operation,
        string Target,
        string Statement,
        int Line,
        string DeclaringTypeFingerprint)
    {
        public bool IsUndeterminable => string.Equals(Target, UndeterminableTarget, StringComparison.Ordinal);
    }

    private sealed record ReviewedDynamicSqlEntry(
        string RelativePath,
        string TypeName,
        string MethodName,
        string DeclaringTypeFingerprint,
        string Rationale)
    {
        public bool MatchesSite(MigrationOccurrence occurrence)
        {
            return occurrence.IsUndeterminable
                && string.Equals(occurrence.RelativePath, RelativePath, StringComparison.Ordinal)
                && string.Equals(occurrence.TypeName, TypeName, StringComparison.Ordinal)
                && string.Equals(occurrence.MethodName, MethodName, StringComparison.Ordinal);
        }

        public bool Matches(MigrationOccurrence occurrence)
        {
            return MatchesSite(occurrence)
                && string.Equals(occurrence.DeclaringTypeFingerprint, DeclaringTypeFingerprint, StringComparison.Ordinal);
        }
    }

    private sealed record MigrationContractEntry(
        string RelativePath,
        string TypeName,
        string MethodName,
        string Operation,
        string Target,
        MigrationContractJustification Justification,
        string IntroducedInVersion,
        string NeverReleasedObject,
        string Rationale)
    {
        public bool Matches(MigrationOccurrence occurrence)
        {
            return string.Equals(occurrence.RelativePath, RelativePath, StringComparison.Ordinal)
                && string.Equals(occurrence.TypeName, TypeName, StringComparison.Ordinal)
                && string.Equals(occurrence.MethodName, MethodName, StringComparison.Ordinal)
                && string.Equals(occurrence.Operation, Operation, StringComparison.Ordinal)
                && string.Equals(occurrence.Target, Target, StringComparison.Ordinal);
        }
    }

    private enum MigrationContractJustification
    {
        ContractPhase,
        NeverReleased,
        ReviewedAdditive,
    }

    private static MetadataReference[] LoadMetadataReferences()
    {
        var references = new Dictionary<string, MetadataReference>(StringComparer.OrdinalIgnoreCase);

        // The test output carries the repository's own assemblies and its NuGet dependencies, but modules compile against
        // the shared frameworks, whose assemblies are not copied locally. Both are needed or nothing binds at all.
        var directories = new List<string>
        {
            AppContext.BaseDirectory,
            Path.GetDirectoryName(typeof(object).Assembly.Location),
        };

        var aspNetCoreFramework = FindAspNetCoreFramework(directories[1]);

        if (aspNetCoreFramework is not null)
        {
            directories.Add(aspNetCoreFramework);
        }

        foreach (var assembly in directories.SelectMany(directory => Directory.EnumerateFiles(directory, "*.dll")))
        {
            string name;

            try
            {
                name = AssemblyName.GetAssemblyName(assembly).Name;
            }
            catch (BadImageFormatException)
            {
                // A native or otherwise unmanaged file in the output folder is not a reference.
                continue;
            }

            if (name is null || references.ContainsKey(name))
            {
                continue;
            }

            references[name] = MetadataReference.CreateFromFile(assembly);
        }

        return references.Values.ToArray();
    }

    private static string FindAspNetCoreFramework(string runtimeDirectory)
    {
        // The ASP.NET Core shared framework sits beside the runtime and carries the logging, options, and dependency
        // injection abstractions every module compiles against. Its version is matched to the runtime's when possible.
        var sharedRoot = Directory.GetParent(runtimeDirectory)?.Parent;

        if (sharedRoot is null)
        {
            return null;
        }

        var framework = Path.Combine(sharedRoot.FullName, "Microsoft.AspNetCore.App");

        if (!Directory.Exists(framework))
        {
            return null;
        }

        var version = new DirectoryInfo(runtimeDirectory).Name;
        var matching = Path.Combine(framework, version);

        return Directory.Exists(matching)
            ? matching
            : Directory
                .EnumerateDirectories(framework)
                .OrderBy(directory => directory, StringComparer.Ordinal)
                .LastOrDefault();
    }

    private sealed record ReconstructionScope(SyntaxNode Node, SemanticModel Model);

}
