import { useMemo, useState, type FormEvent } from 'react';
import { Link } from 'react-router-dom';
import { api, toError } from '../api/client';
import type { OrderDetail } from '../api/types';
import { EmptyState, ErrorMessage, Loading } from '../components/Feedback';
import { useProducts } from '../hooks/useProducts';
import { formatCurrency } from '../lib/format';

/** Seçili ürünler: productId -> miktar. Kayıt yoksa ürün seçili değildir. */
type SelectedItems = Record<number, number>;

export function NewOrderPage() {
  const [search, setSearch] = useState('');
  const { products, isLoading, error: productsError, reload: reloadProducts } = useProducts(search);

  const [customerName, setCustomerName] = useState('');
  const [pricingType, setPricingType] = useState('Standard');
  const [selected, setSelected] = useState<SelectedItems>({});

  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<Error | null>(null);
  const [createdOrder, setCreatedOrder] = useState<OrderDetail | null>(null);
  const [validationMessage, setValidationMessage] = useState<string | null>(null);

  const selectedEntries = useMemo(
    () => Object.entries(selected).map(([id, quantity]) => ({ productId: Number(id), quantity })),
    [selected],
  );

  // Sepet toplamı, seçili ürünlerin güncel fiyatlarıyla önizleme amaçlıdır;
  // kesin tutarı her zaman sunucu hesaplar.
  const estimatedTotal = useMemo(
    () =>
      selectedEntries.reduce((sum, entry) => {
        const product = products.find((p) => p.id === entry.productId);
        return product ? sum + product.price * entry.quantity : sum;
      }, 0),
    [selectedEntries, products],
  );

  const toggleProduct = (productId: number, checked: boolean) => {
    setSelected((current) => {
      const next = { ...current };
      if (checked) {
        next[productId] = 1;
      } else {
        delete next[productId];
      }
      return next;
    });
  };

  const changeQuantity = (productId: number, rawValue: string) => {
    const quantity = Number.parseInt(rawValue, 10);
    setSelected((current) => ({ ...current, [productId]: Number.isNaN(quantity) ? 0 : quantity }));
  };

  const validate = (): string | null => {
    if (customerName.trim().length < 2) {
      return 'Musteri adi en az 2 karakter olmalidir.';
    }
    if (selectedEntries.length === 0) {
      return 'Siparis icin en az bir urun secmelisiniz.';
    }
    if (selectedEntries.some((entry) => entry.quantity <= 0)) {
      return 'Her secili urun icin miktar sifirdan buyuk olmalidir.';
    }

    const overStock = selectedEntries.find((entry) => {
      const product = products.find((p) => p.id === entry.productId);
      return product ? entry.quantity > product.stockQuantity : false;
    });
    if (overStock) {
      const product = products.find((p) => p.id === overStock.productId);
      return `${product?.name} icin mevcut stok ${product?.stockQuantity} adet.`;
    }

    return null;
  };

  const handleSubmit = async (event: FormEvent) => {
    event.preventDefault();
    setCreatedOrder(null);
    setSubmitError(null);

    const validationError = validate();
    setValidationMessage(validationError);
    if (validationError) {
      return;
    }

    setIsSubmitting(true);
    try {
      const order = await api.createOrder({
        customerName: customerName.trim(),
        pricingType,
        items: selectedEntries,
      });

      setCreatedOrder(order);
      setSelected({});
      setCustomerName('');
      // Stoklar degisti: listeyi tazele.
      reloadProducts();
    } catch (err) {
      setSubmitError(toError(err));
      // Sunucu stok hatasi dondurduyse ekrandaki stok bilgisi eskimis olabilir.
      reloadProducts();
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <section>
      <header className="page-header">
        <h1>Yeni Siparis</h1>
        <input
          type="search"
          className="search-input"
          placeholder="Urun ismi veya stok kodu ile ara..."
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          aria-label="Urun ara"
        />
      </header>

      {createdOrder && (
        <div className="state state--success" role="status">
          <strong>
            #{createdOrder.id} numarali siparis olusturuldu. Toplam:{' '}
            {formatCurrency(createdOrder.totalAmount)}
          </strong>
          <p>
            <Link to={`/orders/${createdOrder.id}`}>Siparis detayini goruntule</Link>
          </p>
        </div>
      )}

      {submitError && <ErrorMessage error={submitError} />}
      {validationMessage && (
        <div className="state state--error" role="alert">
          <strong>{validationMessage}</strong>
        </div>
      )}

      {/*
        noValidate: tarayicinin native constraint validation'i (ornegin miktar > max)
        submit'i sessizce engelleyip kendi hata mesajlarimizin gosterilmesini
        onluyordu. Dogrulamayi tek yerde (validate + API) topluyoruz.
      */}
      <form onSubmit={handleSubmit} noValidate>
        <div className="form-row">
          <label>
            Musteri adi
            <input
              type="text"
              value={customerName}
              onChange={(event) => setCustomerName(event.target.value)}
              placeholder="Ornek Musteri"
              maxLength={200}
            />
          </label>

          <label>
            Fiyatlandirma tipi
            <select value={pricingType} onChange={(event) => setPricingType(event.target.value)}>
              <option value="Standard">Standard</option>
              <option value="Bulk">Bulk</option>
            </select>
          </label>
        </div>

        {productsError && <ErrorMessage error={productsError} />}
        {isLoading && <Loading />}
        {!isLoading && !productsError && products.length === 0 && (
          <EmptyState label="Aramanizla eslesen urun bulunamadi." />
        )}

        {!isLoading && !productsError && products.length > 0 && (
          <table>
            <thead>
              <tr>
                <th className="checkbox-column">Sec</th>
                <th>Urun</th>
                <th className="numeric">Fiyat</th>
                <th className="numeric">Stok</th>
                <th className="numeric">Miktar</th>
              </tr>
            </thead>
            <tbody>
              {products.map((product) => {
                const isSelected = product.id in selected;
                const isOutOfStock = product.stockQuantity === 0;

                return (
                  <tr key={product.id} className={isSelected ? 'row--selected' : undefined}>
                    <td className="checkbox-column">
                      <input
                        type="checkbox"
                        checked={isSelected}
                        disabled={isOutOfStock}
                        onChange={(event) => toggleProduct(product.id, event.target.checked)}
                        aria-label={`${product.name} sec`}
                      />
                    </td>
                    <td>
                      {product.name} <code>{product.stockCode}</code>
                    </td>
                    <td className="numeric">{formatCurrency(product.price)}</td>
                    <td className="numeric">
                      {isOutOfStock ? <span className="badge badge--danger">Stokta yok</span> : product.stockQuantity}
                    </td>
                    <td className="numeric">
                      <input
                        type="number"
                        className="quantity-input"
                        min={1}
                        max={product.stockQuantity}
                        value={isSelected ? selected[product.id] : ''}
                        disabled={!isSelected}
                        onChange={(event) => changeQuantity(product.id, event.target.value)}
                        aria-label={`${product.name} miktar`}
                      />
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        )}

        <footer className="form-footer">
          <span>
            {selectedEntries.length} urun secildi &middot; Tahmini toplam:{' '}
            <strong>{formatCurrency(estimatedTotal)}</strong>
          </span>
          <button type="submit" disabled={isSubmitting}>
            {isSubmitting ? 'Gonderiliyor...' : 'Siparisi olustur'}
          </button>
        </footer>
      </form>
    </section>
  );
}
