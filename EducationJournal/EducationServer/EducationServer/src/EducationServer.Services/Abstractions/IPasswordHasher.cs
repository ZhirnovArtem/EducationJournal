namespace EducationServer.Services.Abstractions;

public interface IPasswordHasher
{
    byte[] Hash(string password);
    bool Verify(string password, byte[] stored);
}