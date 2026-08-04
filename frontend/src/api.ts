const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5116';

export interface Product {
  id: number;
  stockCode: string;
  name: string;
  price: number;
  stockQuantity: number;
}

export interface OrderItem {
  productId: number;
  stockCode: string;
  productName: string;
  unitPrice: number;
  quantity: number;
  lineTotal: number;
}

export interface OrderSummary {
  id: number;
  customerName: string;
  pricingType: string;
  createdAtUtc: string;
  totalAmount: number;
  itemCount: number;
}

export interface OrderDetail extends Omit<OrderSummary, 'itemCount'> {
  items: OrderItem[];
}

export interface CreateOrderRequest {
  customerName: string;
  pricingType?: string;
  items: Array<{ productId: number; quantity: number }>;
}

export class ApiError extends Error {
  readonly status: number;
  readonly reasons: string[];

  constructor(message: string, status: number, reasons: string[] = []) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.reasons = reasons;
  }
}

export const toError = (error: unknown): Error =>
  error instanceof Error ? error : new Error('Beklenmeyen bir hata olustu.');

interface ProblemDetails {
  title?: string;
  detail?: string;
  reasons?: string[];
  errors?: Record<string, string[]>;
}

async function toApiError(response: Response): Promise<ApiError> {
  let body: ProblemDetails | null = null;
  try {
    body = (await response.json()) as ProblemDetails;
  } catch {
    body = null;
  }

  const validationMessages = body?.errors ? Object.values(body.errors).flat() : [];

  return new ApiError(
    body?.detail ?? body?.title ?? `Istek basarisiz oldu (HTTP ${response.status}).`,
    response.status,
    body?.reasons ?? validationMessages,
  );
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`${API_BASE_URL}${path}`, {
      headers: { 'Content-Type': 'application/json' },
      ...init,
    });
  } catch {
    throw new ApiError('Sunucuya ulasilamadi. API calisiyor mu?', 0);
  }

  if (!response.ok) {
    throw await toApiError(response);
  }

  return (await response.json()) as T;
}

export const api = {
  getProducts: (search?: string, signal?: AbortSignal) => {
    const query = search?.trim() ? `?search=${encodeURIComponent(search.trim())}` : '';
    return request<Product[]>(`/api/products${query}`, { signal });
  },

  getOrders: () => request<OrderSummary[]>('/api/orders'),

  getOrder: (id: number) => request<OrderDetail>(`/api/orders/${id}`),

  createOrder: (payload: CreateOrderRequest) =>
    request<OrderDetail>('/api/orders', { method: 'POST', body: JSON.stringify(payload) }),
};
