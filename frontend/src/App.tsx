import { NavLink, Navigate, Route, Routes } from 'react-router-dom';
import { NewOrderPage } from './pages/NewOrderPage';
import { OrderDetailPage } from './pages/OrderDetailPage';
import { OrdersPage } from './pages/OrdersPage';
import { ProductsPage } from './pages/ProductsPage';

export default function App() {
  return (
    <div className="app">
      <nav className="app-nav">
        <span className="app-title">Mini Siparis</span>
        <NavLink to="/products">Urunler</NavLink>
        <NavLink to="/orders/new">Yeni Siparis</NavLink>
        <NavLink to="/orders">Siparisler</NavLink>
      </nav>

      <main>
        <Routes>
          <Route path="/" element={<Navigate to="/products" replace />} />
          <Route path="/products" element={<ProductsPage />} />
          <Route path="/orders/new" element={<NewOrderPage />} />
          <Route path="/orders/:id" element={<OrderDetailPage />} />
          <Route path="/orders" element={<OrdersPage />} />
          <Route path="*" element={<p className="state">Sayfa bulunamadi.</p>} />
        </Routes>
      </main>
    </div>
  );
}
