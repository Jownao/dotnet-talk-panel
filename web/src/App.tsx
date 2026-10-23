import { Link, Route, Routes, useParams } from 'react-router-dom'
import { ConversationDetailPage } from './pages/ConversationDetailPage'
import { ConversationListPage } from './pages/ConversationListPage'

// `key={id}` faz o React descartar o estado da tela anterior ao trocar de conversa.
function DetailRoute() {
  const { id } = useParams()
  return <ConversationDetailPage key={id} id={Number(id)} />
}

export default function App() {
  return (
    <div className="app">
      <header className="app-header">
        <Link to="/" className="brand">
          Painel de revisão <span className="muted">· Lingua Viva Idiomas</span>
        </Link>
      </header>
      <main>
        <Routes>
          <Route path="/" element={<ConversationListPage />} />
          <Route path="/conversas/:id" element={<DetailRoute />} />
          <Route path="*" element={<p className="notice">Página não encontrada.</p>} />
        </Routes>
      </main>
    </div>
  )
}
