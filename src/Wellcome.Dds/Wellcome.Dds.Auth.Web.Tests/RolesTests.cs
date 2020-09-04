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
            const string rolesString = "r-w:True|2022-01-01";

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
            const string rolesString = "r-k:True|2022-01-01";

            var expected = new[]
            {
                "https://api.dlcs.io/customers/2/roles/clickthrough",
                "https://api.dlcs.io/customers/2/roles/clinicalImages"
            };
            
            // Act
            var roles = new Roles(rolesString);
            
            // Assert
            roles.IsHealthCareProfessional.Should().BeTrue();
            roles.GetDlcsRoles().Should().ContainInOrder(expected);
        }

        [Fact(Skip = "Temporarily skipped - fix barcodes serialisation")]
        public void CanConvert_ToAndFromString()
        {
            // Arrange
            var sierraRoles = new[] {"k:True", "r:True", "w:True"};
            var roles = new Roles(sierraRoles, DateTime.Today, new string[0]);

            // Act
            var fromString = new Roles(roles.ToString());
            
            // Assert
            fromString.Should().BeEquivalentTo(roles);
        }
    }
}