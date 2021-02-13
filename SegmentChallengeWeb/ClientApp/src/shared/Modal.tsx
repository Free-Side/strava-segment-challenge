import * as React from "react";

type ModalProps = {
    open: boolean,
    title?: string,
    closeModal?: () => void,
    children: any,
};

export function Modal(props: ModalProps) {
    if (props.open) {
        return (
            <div className="modal-dialog-container">
                <div className="modal-dialog">
                    {props.closeModal && <button className="material-icons modal-dialog-close-button" onClick={() => props.closeModal && props.closeModal()}>cancel</button>}
                    {props.title && <header>{props.title}</header>}
                    <section className="modal-dialog-content">
                        {props.children}
                    </section>
                </div>
            </div>
        );
    } else {
        return null;
    }
}

