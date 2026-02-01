using System;
using System.Collections.Generic;
using FlaxEngine;
using System.Security.Cryptography;

namespace OIDDA;

/// <summary>
/// ORSUtils class.
/// </summary>
public class ORSUtils
{
    public enum ORSType 
    {
        ReceiverSender,
        Receiver,
        Sender
    }

    public enum ORSStatus
    {
        Disconnected,
        Connected,
    }

    public static string GeneratedID => GenerateID();

    static string GenerateID()
    {
        const string Characters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        const string SpecialCharacters = "~!@#$%^&*";
        byte[] casualBytes = new byte[5];
        using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(casualBytes);
        char[] result = new char[5];
        for (int i = 0; i < result.Length; i++)
            result[i] = (i != 4) ? Characters[casualBytes[i] % Characters.Length]
                : SpecialCharacters[casualBytes[i] % SpecialCharacters.Length];
        return new string(result);
    }
}
