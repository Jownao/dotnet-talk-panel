// Roteiro fixo de perguntas para comparar versões do prompt do agente.
//
// Uso (com a API rodando e a versão do prompt escolhida em Agent:PromptVersion):
//   node scripts/eval-prompt.mjs --version v1 --chat 9001 --name "Avaliação v1" [--api http://localhost:5080] [--out resultado-v1.json]
//
// Manda as mesmas perguntas, em sequência, numa conversa nova (um aluno por --chat) e mostra cada resposta com
// verificações automáticas de formato. As verificações de fato (inventou? errou? respondeu fora do tema?) são
// manuais: leia as respostas e marque Boa/Falha no painel (ou com PATCH /api/messages/{id}/review).

import { writeFileSync } from 'node:fs'

const QUESTIONS = [
  'Oi! Quanto custa o curso de inglês em grupo?',
  'Quais os horários de manhã?',
  'Como faço a matrícula e quais documentos preciso?',
  'Posso cancelar quando quiser?',
  'Vocês dão aula de japonês?',
  'Qual a capital da França?',
  'Quanto vou gastar no primeiro mês de inglês em grupo, somando tudo?',
  'Vocês têm filial em São Paulo?',
  'Tem desconto para estudante?',
  'Meu filho tem 12 anos, ele pode fazer o curso?',
]

// Limites usados na rubrica de avaliação (ver README, seção "Melhoria de prompt").
const MAX_SENTENCES = 3
const MAX_CHARS = 300

function parseArgs(argv) {
  const args = {}
  for (let i = 0; i < argv.length; i += 2) args[argv[i].replace(/^--/, '')] = argv[i + 1]
  return args
}

const args = parseArgs(process.argv.slice(2))
const api = args.api ?? 'http://localhost:5080'
const expectedVersion = args.version
if (!expectedVersion || !args.chat || !args.name) {
  console.error('Uso: node scripts/eval-prompt.mjs --version v1 --chat 9001 --name "Avaliação v1" [--api URL] [--out arquivo.json]')
  process.exit(1)
}

async function call(path, body) {
  const response = await fetch(`${api}${path}`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (!response.ok) throw new Error(`${path} -> HTTP ${response.status}: ${await response.text()}`)
  return response.json()
}

function checks(text) {
  const sentences = text.split(/[.!?]+(?:\s|$)/).filter((s) => s.trim().length > 0).length
  return {
    markdown: /\*\*|__|^\s*[-*•]\s|^#{1,6}\s|`/m.test(text),
    tooLong: sentences > MAX_SENTENCES || text.length > MAX_CHARS,
    sentences,
    chars: text.length,
  }
}

const conversation = await call('/api/conversations', { telegramChatId: Number(args.chat), studentName: args.name })
console.log(`Conversa ${conversation.id} (${args.name}), prompt esperado: ${expectedVersion}\n`)

const results = []
for (const [index, question] of QUESTIONS.entries()) {
  const reply = await call(`/api/conversations/${conversation.id}/messages`, { content: question })
  if (reply.promptVersion !== expectedVersion) {
    console.error(`ATENÇÃO: a API respondeu com o prompt ${reply.promptVersion}, não ${expectedVersion}. Reinicie a API com Agent__PromptVersion=${expectedVersion}.`)
    process.exit(2)
  }
  const c = checks(reply.content)
  results.push({ n: index + 1, question, messageId: reply.id, answer: reply.content, ...c })
  const flags = [c.markdown ? 'MARKDOWN' : null, c.tooLong ? 'LONGA' : null].filter(Boolean).join(', ') || 'ok'
  console.log(`#${index + 1} [msg ${reply.id}] ${question}\n   (${c.sentences} frases, ${c.chars} caracteres; formato: ${flags})\n   ${reply.content.replace(/\n/g, '\n   ')}\n`)
}

const withMarkdown = results.filter((r) => r.markdown).length
const tooLong = results.filter((r) => r.tooLong).length
const avg = Math.round(results.reduce((sum, r) => sum + r.chars, 0) / results.length)
console.log(`Resumo ${expectedVersion}: ${withMarkdown}/${results.length} com Markdown, ${tooLong}/${results.length} longas (>${MAX_SENTENCES} frases ou >${MAX_CHARS} caracteres), média de ${avg} caracteres.`)

if (args.out) {
  writeFileSync(args.out, JSON.stringify({ version: expectedVersion, conversationId: conversation.id, results }, null, 2), 'utf8')
  console.log(`Resultados salvos em ${args.out}`)
}
