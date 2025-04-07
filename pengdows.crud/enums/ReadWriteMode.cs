namespace pengdows.crud;

[Flags]
public enum ReadWriteMode
{
    ReadOnly = 1,
    WriteOnly = 2,
    ReadWrite = 3
}