import { useState } from 'react';
import { ErrorMessage } from '../ErrorMessage';
import { formatCurrency } from '../format';
import { useProducts } from '../useProducts';

export function ProductsPage() {
  const [search, setSearch] = useState('');
  const { products, isLoading, error } = useProducts(search);

  return (
    <section>
      <header className="page-header">
        <h1>Urunler</h1>
        <input
          type="search"
          className="search-input"
          placeholder="Urun ismi veya stok kodu ile ara..."
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          aria-label="Urun ara"
        />
      </header>

      {error && <ErrorMessage error={error} />}
      {isLoading && <p className="state state--loading">Yukleniyor...</p>}

      {!isLoading && !error && products.length === 0 && (
        <p className="state">Aramanizla eslesen urun bulunamadi.</p>
      )}

      {!isLoading && !error && products.length > 0 && (
        <table>
          <thead>
            <tr>
              <th>Stok Kodu</th>
              <th>Urun</th>
              <th className="numeric">Fiyat</th>
              <th className="numeric">Stok</th>
            </tr>
          </thead>
          <tbody>
            {products.map((product) => (
              <tr key={product.id}>
                <td><code>{product.stockCode}</code></td>
                <td>{product.name}</td>
                <td className="numeric">{formatCurrency(product.price)}</td>
                <td className="numeric">
                  {product.stockQuantity === 0 ? (
                    <span className="badge badge--danger">Stokta yok</span>
                  ) : (
                    product.stockQuantity
                  )}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </section>
  );
}
