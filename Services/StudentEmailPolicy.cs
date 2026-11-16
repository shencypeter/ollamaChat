using System.ComponentModel.DataAnnotations;
namespace AiTeachingAssistant.Services;
public static class StudentEmailPolicy
{
    public const string Domain="csmu.edu.tw";
    public static string InstitutionalAddress(string email)
    {
        var address=email.Trim();var at=address.LastIndexOf('@');
        if(at<1||!new EmailAddressAttribute().IsValid(address))throw new ArgumentException("A valid existing email address is required.");
        return address[..at].ToLowerInvariant()+"@"+Domain;
    }
}
