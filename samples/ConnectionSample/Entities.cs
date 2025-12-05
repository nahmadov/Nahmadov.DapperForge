using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ConnectionSample;

public class User
{
    [Key()]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    [Column("USERID")]
    public int Id { get; set; }

    [Required, MaxLength(100)]
    [Column("username")]
    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}

[Table("Logs", Schema = "dbo")]
public class LogEntry
{
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int Id { get; set; }

    public DateTime CreatedDate { get; set; }

    [MaxLength(256)]
    public string Message { get; set; } = string.Empty;
}
