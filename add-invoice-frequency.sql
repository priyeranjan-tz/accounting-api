-- Add invoice_frequency column to accounts table
-- This fixes the missing column issue

-- Check if column exists before adding
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_name = 'accounts'
          AND column_name = 'invoice_frequency'
    ) THEN
        ALTER TABLE accounts
        ADD COLUMN invoice_frequency integer NOT NULL DEFAULT 3;  -- 3 = Monthly (InvoiceFrequency.Monthly)
        
        RAISE NOTICE 'Column invoice_frequency added successfully';
    ELSE
        RAISE NOTICE 'Column invoice_frequency already exists';
    END IF;
END $$;
