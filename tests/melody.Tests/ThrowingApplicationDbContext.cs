using melody.Data;
using Microsoft.EntityFrameworkCore;

namespace melody.Tests;

/// <summary>
/// A test-only <see cref="ApplicationDbContext"/> subclass that throws a <see cref="DbUpdateException"/>
/// from <see cref="SaveChangesAsync"/> exactly once, only when armed by the test, so seeding and
/// unrelated requests behave normally.
/// </summary>
/// <remarks>
/// The armed flag is a plain static, not an <see cref="AsyncLocal{T}"/> or an instance field.
/// An <see cref="AsyncLocal{T}"/> set in the test method does not flow into the request: since
/// ASP.NET Core 3.0, <c>TestServer</c> no longer preserves the caller's <see
/// cref="System.Threading.ExecutionContext"/> when dispatching a request (confirmed empirically
/// here — the armed flag never reached <see cref="SaveChangesAsync"/> and every request redirected
/// as a successful save). An instance-level flag is also unreachable from the test: each HTTP
/// request resolves its own scoped <see cref="ApplicationDbContext"/>, and the test has no handle
/// on that specific instance before the request runs. A plain static works because this type is
/// only ever used by <c>SwallowedExceptionLoggingTests</c> (via <see
/// cref="FaultInjectingWebApplicationFactory"/>, never <c>MelodyWebApplicationFactory</c>'s 14
/// other consumer test files), and xUnit runs the <c>[Fact]</c> methods within one test class
/// sequentially by default, so no other test can be mid-request when a given test arms the flag.
/// </remarks>
public class ThrowingApplicationDbContext : ApplicationDbContext
{
    private static bool _armed;
    private static int _savesToSkip;

    public ThrowingApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    /// <summary>
    /// Set to <c>true</c> immediately before the one request a test wants to fail, to throw on
    /// that request's very next <see cref="SaveChangesAsync"/> call. Reset to <c>false</c>
    /// automatically once it throws.
    /// </summary>
    public static bool ThrowOnNextSave
    {
        get => _armed;
        set => Arm(skipSaves: 0, armed: value);
    }

    /// <summary>
    /// Arms the fault for the <paramref name="skipSaves"/>-th+1 <see cref="SaveChangesAsync"/>
    /// call after arming (0 = the very next call). Needed when the request under test calls
    /// <c>SaveChangesAsync</c> one or more times before reaching the catch block under test — e.g.
    /// <c>AdminTeachersController.Create</c>'s <c>UserManager.CreateAsync</c>/<c>AddToRoleAsync</c>
    /// calls each save the same <see cref="ApplicationDbContext"/> before the controller's own
    /// <c>SaveChangesAsync</c> call inside its <c>try</c> block.
    /// </summary>
    public static void Arm(int skipSaves = 0, bool armed = true)
    {
        _armed = armed;
        _savesToSkip = skipSaves;
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (_armed)
        {
            if (_savesToSkip > 0)
            {
                _savesToSkip--;
            }
            else
            {
                _armed = false;
                throw new DbUpdateException(
                    "Simulated failure for test",
                    new InvalidOperationException("Simulated inner DB failure"));
            }
        }

        return base.SaveChangesAsync(cancellationToken);
    }
}
