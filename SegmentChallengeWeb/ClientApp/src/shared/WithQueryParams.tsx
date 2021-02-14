import * as React from 'react';
import { useLocation } from "react-router";

export const withQueryParams = (Component: any) => {
  return (props: any) => {
    const location = useLocation();
    const queryParams = new URLSearchParams(location.search);

    return (<Component queryParams={queryParams} location={location} {...props} />);
  };
};

export interface IQueryParamsProps {
  location: Location,
  queryParams: URLSearchParams
}
