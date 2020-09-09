using System;
using FluentAssertions;
using Xunit;

namespace Wellcome.Dds.Auth.Web.Tests
{
    public class RolesTests
    {
        [Fact]
        public void GetDlcsRoles_HasAllRoles_IfWellcomeStaffMember()
        {
            // Arrange
            const string rolesString = "r-w:True|e-2022-01-01|bbbb";

            var expected = new[]
            {
                "https://api.dlcs.io/customers/2/roles/clickthrough",
                "https://api.dlcs.io/customers/2/roles/clinicalImages",
                "https://api.dlcs.io/customers/2/roles/restrictedFiles"
            };
            
            // Act
            var roles = new Roles(rolesString);
            
            // Assert
            roles.IsWellcomeStaffMember.Should().BeTrue();
            roles.GetDlcsRoles().Should().ContainInOrder(expected);
        }
        
        [Fact]
        public void GetDlcsRoles_HasClinicalAndClickThroughRoles_IfWellcomeStaffMember()
        {
            // Arrange
            const string rolesString = "r-k:True|e-2022-01-01|b-123456";

            var expectedRoles = new[]
            {
                "https://api.dlcs.io/customers/2/roles/clickthrough",
                "https://api.dlcs.io/customers/2/roles/clinicalImages"
            };
            var expectedBarcodes = new[] {"123456"};
            
            // Act
            var roles = new Roles(rolesString);
            
            // Assert
            roles.IsHealthCareProfessional.Should().BeTrue();
            roles.GetDlcsRoles().Should().ContainInOrder(expectedRoles);
            roles.BarCodes.Should().ContainInOrder(expectedBarcodes);
        }

        
        
        [Fact]
        public void CanConvert_ToAndFromString()
        {
            // Arrange
            var sierraRoles = new[] {"k:True", "r:True", "w:True"};
            var barCodes = new[] {"123", "456", "789"};
            var roles = new Roles(sierraRoles, DateTime.Today, barCodes);

            // Act
            var fromString = new Roles(roles.ToString());
            
            // Assert
            fromString.Should().BeEquivalentTo(roles);
        }
    }
}