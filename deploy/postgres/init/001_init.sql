ALTER TABLE IF EXISTS orders
    ADD COLUMN IF NOT EXISTS order_type TEXT NOT NULL DEFAULT 'CUSTOMER';

UPDATE orders
SET order_type = 'CUSTOMER'
WHERE order_type IS NULL OR order_type = '';

DO $$
BEGIN
    IF EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = current_schema()
          AND table_name = 'orders'
          AND column_name = 'partner_id'
          AND is_nullable = 'NO'
    ) THEN
        ALTER TABLE orders ALTER COLUMN partner_id DROP NOT NULL;
    END IF;
END $$;
