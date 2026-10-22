using System.ComponentModel.DataAnnotations;

namespace Api.Models.Dtos;

public record SendMessageRequest([Required, StringLength(2000)] string Content);
