package _5_ManagementSystems._2_LibraryManagementSystem.services;

import _5_ManagementSystems._2_LibraryManagementSystem.entities.Book;
import _5_ManagementSystems._2_LibraryManagementSystem.entities.search.SearchCriteria;

import java.util.Collection;
import java.util.List;
import java.util.stream.Collectors;

public class BookSearchService {
    public List<Book> search(Collection<Book> catalog, SearchCriteria criteria){
        return catalog.stream()
                .filter(b -> criteria.getAuthor() == null ||
                        b.getAuthor().toLowerCase().contains(criteria.getAuthor().toLowerCase()))
                .filter(b->criteria.getTitle() == null ||
                        b.getTitle().toLowerCase().contains(criteria.getTitle().toLowerCase()))
                .filter(b->criteria.getPublicationYear() == null ||
                        b.getPublicationYear() == criteria.getPublicationYear())
                .filter(b->criteria.getAvailableOnly()==null ||
                        criteria.getAvailableOnly() == false ||
                        b.getAvailableCopies() > 0)
                .collect(Collectors.toList());
    }
}
