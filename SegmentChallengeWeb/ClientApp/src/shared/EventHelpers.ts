import * as React from 'react';

export function onEnterKey<T>(fn: (e: React.KeyboardEvent<T>) => void): (e: React.KeyboardEvent<T>) => void {
    return (e: React.KeyboardEvent<T>) => {
        if (e.key === 'Enter') {
            fn(e);
        }
    };
}
