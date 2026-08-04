import { Link, NavLink, Navigate, Route, Routes } from 'react-router-dom';
import { NewOrderPage } from './pages/NewOrderPage';
import { OrderDetailPage } from './pages/OrderDetailPage';
import { OrdersPage } from './pages/OrdersPage';
import { ProductsPage } from './pages/ProductsPage';

function NotFoundPage() {
  return (
    <section>
      <header className="page-header">
        <h1>Sayfa bulunamadi</h1>
      </header>
      <p className="state">
        Aradiginiz sayfa yok. <Link to="/products">Urun listesine donebilirsiniz.</Link>
      </p>
    </section>
  );
}

export default function App() {
  return (
    <div className="app">
      <a className="skip-link" href="#main">
        Icerige atla
      </a>

      <nav className="app-nav">
        <Link to="/products" className="app-title">
          Mini Siparis
        </Link>
        <NavLink to="/products">Urunler</NavLink>
        <NavLink to="/orders/new">Yeni siparis</NavLink>
        {/* end: /orders, /orders/new adresinde de aktif gorunmesin. */}
        <NavLink to="/orders" end>
          Siparisler
        </NavLink>
      </nav>

      <main id="main">
        <Routes>
          <Route path="/" element={<Navigate to="/products" replace />} />
          <Route path="/products" element={<ProductsPage />} />
          <Route path="/orders/new" element={<NewOrderPage />} />
          <Route path="/orders/:id" element={<OrderDetailPage />} />
          <Route path="/orders" element={<OrdersPage />} />
          <Route path="*" element={<NotFoundPage />} />
        </Routes>
      </main>
    </div>
  );
}
