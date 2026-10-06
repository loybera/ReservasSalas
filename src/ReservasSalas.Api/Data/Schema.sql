-- Esquema idempotente: se puede ejecutar en cada arranque sin efectos secundarios.

-- Permite combinar igualdad (sala_id) y superposición de rangos en un mismo índice GiST.
CREATE EXTENSION IF NOT EXISTS btree_gist;

CREATE TABLE IF NOT EXISTS salas (
    id        uuid         PRIMARY KEY,
    nombre    varchar(100) NOT NULL,
    capacidad integer      NOT NULL CHECK (capacidad > 0)
);

-- Nombres únicos sin distinguir mayúsculas ("Sala A" y "sala a" son la misma).
CREATE UNIQUE INDEX IF NOT EXISTS ux_salas_nombre ON salas (lower(nombre));

CREATE TABLE IF NOT EXISTS reservas (
    id                uuid         PRIMARY KEY,
    sala_id           uuid         NOT NULL REFERENCES salas (id),
    inicio            timestamptz  NOT NULL,
    fin               timestamptz  NOT NULL,
    organizador_email varchar(254) NOT NULL,
    asistentes        integer      NOT NULL CHECK (asistentes > 0),
    creada_en         timestamptz  NOT NULL,
    cancelada_en      timestamptz  NULL,
    CONSTRAINT ck_reservas_fin_posterior CHECK (fin > inicio)
);

-- La regla central del negocio: dos reservas activas de la misma sala no pueden superponerse.
-- '[)' = el fin no se incluye, así una reserva que termina a las 10:00 no choca con otra
-- que empieza a las 10:00. Las canceladas quedan fuera de la restricción.
-- Al vivir en la base, se cumple aunque lleguen requests simultáneos o haya varias instancias de la API.
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ex_reservas_sin_superposicion') THEN
        ALTER TABLE reservas
            ADD CONSTRAINT ex_reservas_sin_superposicion
            EXCLUDE USING gist (
                sala_id WITH =,
                tstzrange(inicio, fin, '[)') WITH &&
            )
            WHERE (cancelada_en IS NULL);
    END IF;
END $$;
