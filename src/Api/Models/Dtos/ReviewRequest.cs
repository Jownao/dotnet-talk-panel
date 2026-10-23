using System.ComponentModel.DataAnnotations;
using Api.Models.Entities;

namespace Api.Models.Dtos;

/// <summary>Avaliação de uma resposta do agente. Rating é nullable de propósito: sem ele o [Required] devolve 400 em vez de assumir "Good".</summary>
public record ReviewRequest(
    [Required, EnumDataType(typeof(MessageRating))] MessageRating? Rating,
    [StringLength(1000)] string? Comment);
